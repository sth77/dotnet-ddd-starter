using App.Domain.Common;

namespace App.Domain.Todo;

/// <summary>Closed hierarchy of events published by <see cref="Todo"/>.</summary>
public abstract record TodoEvent : IDomainEvent
{
    private TodoEvent()
    {
    }

    public sealed record Created(TodoId TodoId) : TodoEvent;

    public sealed record Updated(TodoId TodoId, string Name, string Description) : TodoEvent;

    public sealed record Activated(TodoId TodoId) : TodoEvent;

    public sealed record Closed(TodoId TodoId) : TodoEvent;
}
