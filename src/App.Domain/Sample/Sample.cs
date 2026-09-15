using App.Domain.Common;
using App.Domain.Person;
using RefCity = App.Domain.ReferenceData.City;

namespace App.Domain.Sample;

/// <summary>
/// The sample aggregate. One command record per operation, a <see cref="Can{TCommand}"/> guard that doubles
/// as the HAL link predicate, and events registered rather than published.
/// </summary>
public sealed class Sample : AggregateRoot<Sample, SampleId>
{
    private Sample(
        SampleId id,
        I18nText name,
        string description,
        City? city,
        Association<Person.Person, PersonId> owner,
        string ownerName,
        DateTimeOffset createdAt)
        : base(id)
    {
        Name = name;
        Description = description;
        City = city;
        Owner = owner;
        OwnerName = ownerName;
        CreatedAt = createdAt;
        State = SampleState.Draft;
    }

#pragma warning disable CS8618 // Materialisation constructor: EF Core sets the remaining properties after construction.
    private Sample(SampleId id, Association<Person.Person, PersonId> owner, DateTimeOffset createdAt)
        : base(id)
    {
        Owner = owner;
        CreatedAt = createdAt;
    }
#pragma warning restore CS8618

    public I18nText Name { get; private set; }

    public string Description { get; private set; }

    /// <summary>Copied from reference data at creation/update time; <c>null</c> when no city was given.</summary>
    public City? City { get; private set; }

    public SampleState State { get; private set; }

    /// <summary>Reference into the Person module, by identity only.</summary>
    public Association<Person.Person, PersonId> Owner { get; }

    /// <summary>Denormalised copy of the owner's name, kept in sync by <c>SampleOwnerNameSynchronizer</c>.</summary>
    public string OwnerName { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public static Sample Create(SampleCommand.Create data, RefCity? city, Person.Person owner, TimeProvider clock)
    {
        var sample = new Sample(
            SampleId.New(),
            data.Name,
            data.Description,
            city is null ? null : City.From(city),
            Association<Person.Person, PersonId>.To(owner),
            owner.Name,
            clock.GetUtcNow());

        sample.RegisterEvent(new SampleEvent.Created(sample.Id, sample.Owner));
        return sample;
    }

    public void Update(SampleCommand.Update data, RefCity? city)
    {
        AssertCan<SampleCommand.Update>();

        Name = data.Name;
        Description = data.Description;
        City = city is null ? null : City.From(city);

        RegisterEvent(new SampleEvent.Updated(Id, Name, Description));
    }

    public void Publish(SampleCommand.Publish data)
    {
        AssertCan<SampleCommand.Publish>();

        State = SampleState.Published;
        RegisterEvent(new SampleEvent.Published(Id));
    }

    public void Archive(SampleCommand.Archive data)
    {
        AssertCan<SampleCommand.Archive>();

        State = SampleState.Archived;
        RegisterEvent(new SampleEvent.Archived(Id));
    }

    public void UpdateOwnerName(SampleCommand.UpdateOwnerName data)
    {
        AssertCan<SampleCommand.UpdateOwnerName>();

        OwnerName = data.OwnerName;
    }

    /// <summary>Whether the aggregate's current state permits the given command. Drives HAL link visibility.</summary>
    public bool Can<TCommand>()
        where TCommand : SampleCommand
        => Can(typeof(TCommand));

    public bool Can(Type command) => State switch
    {
        SampleState.Draft => command == typeof(SampleCommand.Update)
                             || command == typeof(SampleCommand.Publish)
                             || command == typeof(SampleCommand.UpdateOwnerName),
        SampleState.Published => command == typeof(SampleCommand.Update)
                                 || command == typeof(SampleCommand.Archive)
                                 || command == typeof(SampleCommand.UpdateOwnerName),
        SampleState.Archived => command == typeof(SampleCommand.UpdateOwnerName),
        _ => false,
    };

    private void AssertCan<TCommand>()
        where TCommand : SampleCommand
    {
        if (!Can<TCommand>())
        {
            throw new OperationNotAllowedException(typeof(Sample), typeof(TCommand), State.ToString());
        }
    }
}
