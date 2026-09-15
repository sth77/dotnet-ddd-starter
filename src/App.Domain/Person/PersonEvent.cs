using App.Domain.Common;

namespace App.Domain.Person;

public abstract record PersonEvent : IDomainEvent
{
    private PersonEvent()
    {
    }

    public sealed record Created(PersonId PersonId, string Name) : PersonEvent;

    /// <summary>Consumed by the Sample module to refresh its denormalised owner name.</summary>
    public sealed record Updated(PersonId PersonId, string Name) : PersonEvent;
}
