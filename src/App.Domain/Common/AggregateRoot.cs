namespace App.Domain.Common;

/// <summary>
/// Base class for aggregate roots: identity, identity-based equality and the registered-events list
/// (the analogue of Spring's <c>AbstractAggregateRoot</c>). Optimistic concurrency is configured once in the
/// DbContext (PostgreSQL <c>xmin</c>), so no aggregate carries a version property.
/// </summary>
public abstract class AggregateRoot<TSelf, TId> : IAggregateRoot<TSelf, TId>, IHasDomainEvents, IEquatable<TSelf>
    where TSelf : AggregateRoot<TSelf, TId>
    where TId : IIdentifier
{
    private readonly List<IDomainEvent> _events = [];

    protected AggregateRoot(TId id)
    {
        Id = id;
    }

    public TId Id { get; }

    IReadOnlyList<IDomainEvent> IHasDomainEvents.DomainEvents => _events;

    void IHasDomainEvents.ClearDomainEvents() => _events.Clear();

    /// <summary>Records an event to be published once this aggregate is successfully committed.</summary>
    protected void RegisterEvent(IDomainEvent domainEvent) => _events.Add(domainEvent);

    /// <summary>The events registered since the aggregate was loaded or last committed. For tests and assertions.</summary>
    public IReadOnlyList<IDomainEvent> PendingEvents => _events;

    public bool Equals(TSelf? other) => other is not null && EqualityComparer<TId>.Default.Equals(Id, other.Id);

    public override bool Equals(object? obj) => obj is TSelf other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(typeof(TSelf), Id);

    public override string ToString() => $"{typeof(TSelf).Name}({Id})";
}
