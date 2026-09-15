using App.Domain.Common;

namespace App.Application.Common;

/// <summary>
/// Handles one domain event, asynchronously, after the publishing aggregate was committed, in its own unit of
/// work (the <c>@ApplicationModuleListener</c> role). Implementations are plain classes discovered by the
/// composition root; they must be <b>idempotent</b>, because the outbox redelivers after a crash and the inbox
/// only de-duplicates per handler once the handler's own transaction committed.
/// </summary>
public interface IEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken ct);
}
