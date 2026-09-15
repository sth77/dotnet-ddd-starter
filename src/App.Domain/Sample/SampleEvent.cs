using App.Domain.Common;
using App.Domain.Person;

namespace App.Domain.Sample;

/// <summary>Closed hierarchy of events published by <see cref="Sample"/>.</summary>
public abstract record SampleEvent : IDomainEvent
{
    private SampleEvent()
    {
    }

    public sealed record Created(SampleId SampleId, Association<Person.Person, PersonId> Owner) : SampleEvent;

    public sealed record Updated(SampleId SampleId, I18nText Name, string Description) : SampleEvent;

    public sealed record Published(SampleId SampleId) : SampleEvent;

    public sealed record Archived(SampleId SampleId) : SampleEvent;
}
