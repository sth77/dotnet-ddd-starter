using System.Text.Json;
using App.Domain.Common;

namespace App.Infrastructure.Messaging;

/// <summary>
/// A domain event waiting to be delivered. Written in the same transaction as the aggregate that raised it
/// (transactional outbox), claimed and dispatched by <see cref="OutboxDispatcher"/>.
/// </summary>
public sealed class OutboxMessage
{
    private OutboxMessage(Guid id, string type, string payload, DateTimeOffset occurredAt)
    {
        Id = id;
        Type = type;
        Payload = payload;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private set; }

    /// <summary>CLR full name of the event type; resolved through <see cref="DomainEventTypes"/>.</summary>
    public string Type { get; private set; }

    public string Payload { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public int Attempts { get; private set; }

    /// <summary>Set while a dispatcher instance works on the message; expired locks are reclaimable.</summary>
    public DateTimeOffset? LockedUntil { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    /// <summary>Dead letter: gave up after the maximum number of attempts. Needs a human.</summary>
    public DateTimeOffset? FailedAt { get; private set; }

    public string? LastError { get; private set; }

    public static OutboxMessage From(IDomainEvent domainEvent, DateTimeOffset occurredAt)
    {
        var type = domainEvent.GetType();
        return new OutboxMessage(
            Guid.CreateVersion7(),
            DomainEventTypes.NameOf(type),
            JsonSerializer.Serialize(domainEvent, type, DomainEventTypes.SerializerOptions),
            occurredAt);
    }
}

/// <summary>
/// Records that a handler has processed a message, in the handler's own transaction, so redelivery is a no-op
/// (idempotent consumption). One row per (message, handler).
/// </summary>
public sealed class InboxMessage(Guid messageId, string handler, DateTimeOffset processedAt)
{
    public Guid MessageId { get; private set; } = messageId;

    public string Handler { get; private set; } = handler;

    public DateTimeOffset ProcessedAt { get; private set; } = processedAt;
}
