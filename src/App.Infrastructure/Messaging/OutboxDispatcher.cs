using System.Reflection;
using System.Text.Json;
using App.Application.Common;
using App.Domain.Common;
using App.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace App.Infrastructure.Messaging;

/// <summary>
/// Delivers outbox messages to their <see cref="IEventHandler{TEvent}"/>s (the Spring Modulith event
/// publication registry, hand-rolled — ADR-008):
/// <list type="number">
/// <item><b>Claim</b> a batch with <c>FOR UPDATE SKIP LOCKED</c>, so several replicas can run this service.</item>
/// <item><b>Dispatch</b> each message to each handler in its own scope and database transaction. The inbox row
/// is written in that same transaction, so a redelivered message is skipped per handler that already succeeded.</item>
/// <item><b>Mark</b> the message processed, or schedule a retry with cooldown; after <see cref="OutboxOptions.MaxAttempts"/>
/// it is dead-lettered and logged as an error.</item>
/// </list>
/// </summary>
internal sealed partial class OutboxDispatcher(
    IServiceScopeFactory scopes,
    IOptions<OutboxOptions> options,
    OutboxSignal signal,
    TimeProvider clock,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    private readonly OutboxOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = 0;
            try
            {
                processed = await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogBatchFailed(ex);
            }

            if (processed == 0)
            {
                await signal.WaitAsync(_options.PollInterval, stoppingToken);
            }
        }
    }

    /// <summary>Visible for tests: one claim-dispatch-mark cycle; returns the number of messages handled.</summary>
    internal async Task<int> ProcessBatchAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var claimed = await ClaimAsync(db, ct);
        foreach (var message in claimed)
        {
            var error = await DispatchAsync(message, ct);
            var now = clock.GetUtcNow();

            if (error is null)
            {
                await db.Set<OutboxMessage>().Where(m => m.Id == message.Id).ExecuteUpdateAsync(
                    s => s.SetProperty(m => m.ProcessedAt, now).SetProperty(m => m.LockedUntil, (DateTimeOffset?)null), ct);
                continue;
            }

            var deadLetter = message.Attempts >= _options.MaxAttempts;
            var retryAt = now + _options.BackoffFor(message.Attempts);
            await db.Set<OutboxMessage>().Where(m => m.Id == message.Id).ExecuteUpdateAsync(
                s => s.SetProperty(m => m.LastError, error)
                    .SetProperty(m => m.LockedUntil, deadLetter ? null : retryAt)
                    .SetProperty(m => m.FailedAt, deadLetter ? now : null), ct);

            if (deadLetter)
            {
                LogDeadLettered(message.Id, message.Type, message.Attempts, error);
            }
            else
            {
                LogRetryScheduled(message.Id, message.Type, message.Attempts, retryAt, error);
            }
        }

        return claimed.Count;
    }

    private async Task<IReadOnlyList<ClaimedMessage>> ClaimAsync(AppDbContext db, CancellationToken ct)
    {
        // Data-modifying CTEs cannot be composed by EF Core; plain ADO.NET on the context's connection.
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync(ct);

        var now = clock.GetUtcNow();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE outbox_messages
            SET locked_until = @until, attempts = attempts + 1
            WHERE id IN (
                SELECT id FROM outbox_messages
                WHERE processed_at IS NULL AND failed_at IS NULL AND (locked_until IS NULL OR locked_until < @now)
                ORDER BY occurred_at
                LIMIT @batch
                FOR UPDATE SKIP LOCKED)
            RETURNING id, type, payload, attempts
            """;
        command.Parameters.AddWithValue("until", now + _options.LockDuration);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("batch", _options.BatchSize);

        var claimed = new List<ClaimedMessage>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            claimed.Add(new ClaimedMessage(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3)));
        }

        return claimed;
    }

    /// <returns><c>null</c> on success, otherwise the error to record.</returns>
    private async Task<string?> DispatchAsync(ClaimedMessage message, CancellationToken ct)
    {
        Type eventType;
        IDomainEvent domainEvent;
        try
        {
            eventType = DomainEventTypes.Resolve(message.Type);
            domainEvent = (IDomainEvent)JsonSerializer.Deserialize(message.Payload, eventType, DomainEventTypes.SerializerOptions)!;
        }
        catch (Exception ex) when (ex is InvalidOperationException or JsonException)
        {
            return ex.ToString();
        }

        var handlerInterface = typeof(IEventHandler<>).MakeGenericType(eventType);
        var handle = handlerInterface.GetMethod(nameof(IEventHandler<>.HandleAsync))!;

        IReadOnlyList<Type> handlerTypes;
        await using (var probe = scopes.CreateAsyncScope())
        {
            handlerTypes = probe.ServiceProvider.GetServices(handlerInterface).Select(h => h!.GetType()).ToList();
        }

        foreach (var handlerType in handlerTypes)
        {
            try
            {
                await InvokeHandlerAsync(message.Id, domainEvent, handlerInterface, handlerType, handle, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogHandlerFailed(ex, handlerType.Name, message.Id, message.Type, message.Attempts);
                return $"{handlerType.FullName}: {ex}";
            }
        }

        return null;
    }

    private async Task InvokeHandlerAsync(Guid messageId, IDomainEvent domainEvent, Type handlerInterface, Type handlerType, MethodInfo handle, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var handlerName = handlerType.FullName!;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        if (await db.Set<InboxMessage>().AnyAsync(i => i.MessageId == messageId && i.Handler == handlerName, ct))
        {
            return; // already processed by this handler: redelivery after a crash between commit and mark
        }

        var handler = scope.ServiceProvider.GetServices(handlerInterface).First(h => h!.GetType() == handlerType)!;
        try
        {
            await (Task)handle.Invoke(handler, [domainEvent, ct])!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }

        db.Set<InboxMessage>().Add(new InboxMessage(messageId, handlerName, clock.GetUtcNow()));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private sealed record ClaimedMessage(Guid Id, string Type, string Payload, int Attempts);

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox batch failed; will retry after the poll interval")]
    partial void LogBatchFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Handler {Handler} failed for outbox message {MessageId} ({Type}), attempt {Attempt}")]
    partial void LogHandlerFailed(Exception exception, string handler, Guid messageId, string type, int attempt);

    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox message {MessageId} ({Type}) attempt {Attempt} failed; retry at {RetryAt}: {Error}")]
    partial void LogRetryScheduled(Guid messageId, string type, int attempt, DateTimeOffset retryAt, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox message {MessageId} ({Type}) dead-lettered after {Attempts} attempts: {Error}")]
    partial void LogDeadLettered(Guid messageId, string type, int attempts, string error);
}
