using App.Domain.Common;
using App.Infrastructure.Messaging;

namespace App.Infrastructure.Persistence;

/// <summary>
/// Commits the DbContext and writes the domain events registered on tracked aggregates to the outbox table in
/// the same transaction (design §7). Events are cleared only after the commit succeeded; the dispatcher is
/// signalled afterwards so delivery starts immediately.
/// </summary>
internal sealed class EfUnitOfWork(AppDbContext db, TimeProvider clock, OutboxSignal signal) : IUnitOfWork
{
    public async Task CommitAsync(CancellationToken ct = default)
    {
        var aggregates = db.ChangeTracker.Entries()
            .Select(entry => entry.Entity)
            .OfType<IHasDomainEvents>()
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .ToList();

        var occurredAt = clock.GetUtcNow();
        foreach (var domainEvent in aggregates.SelectMany(aggregate => aggregate.DomainEvents))
        {
            db.Set<OutboxMessage>().Add(OutboxMessage.From(domainEvent, occurredAt));
        }

        await db.SaveChangesAsync(ct);

        foreach (var aggregate in aggregates)
        {
            aggregate.ClearDomainEvents();
        }

        if (aggregates.Count > 0)
        {
            signal.Notify();
        }
    }
}
