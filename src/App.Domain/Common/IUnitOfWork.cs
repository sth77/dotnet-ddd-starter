namespace App.Domain.Common;

/// <summary>
/// Commits every change made through the repositories in the current scope and publishes the domain events
/// registered on the committed aggregates, atomically (transactional outbox). Endpoints and event handlers
/// call it exactly once per unit of work.
/// </summary>
public interface IUnitOfWork
{
    Task CommitAsync(CancellationToken ct = default);
}
