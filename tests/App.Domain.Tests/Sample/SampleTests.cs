using App.Domain.Common;
using App.Domain.Person;
using App.Domain.ReferenceData;
using App.Domain.Sample;
using Microsoft.Extensions.Time.Testing;
using RefCity = App.Domain.ReferenceData.City;

namespace App.Domain.Tests;

public sealed class SampleTests
{
    private static readonly FakeTimeProvider Clock = new(new DateTimeOffset(2026, 9, 12, 10, 0, 0, TimeSpan.Zero));

    private static readonly RefCity Bern = new(CityId.New(), 3000, new I18nText("Bern", "Bern"));

    private static Person.Person Owner() => Person.Person.Create(new PersonCommand.Create("Ada Lovelace", EmailAddress.From("ada@example.org")));

    private static Domain.Sample.Sample Draft(Person.Person? owner = null, RefCity? city = null)
        => Domain.Sample.Sample.Create(
            new SampleCommand.Create(new I18nText("Name", "Name"), "Description", city?.Id, (owner ?? Owner()).Id),
            city,
            owner ?? Owner(),
            Clock);

    [Fact]
    public void Create_copies_owner_name_and_city_fields_and_registers_created_event()
    {
        var owner = Owner();

        var sample = Draft(owner, Bern);

        Assert.Equal(SampleState.Draft, sample.State);
        Assert.Equal(Association<Person.Person, PersonId>.To(owner), sample.Owner);
        Assert.Equal("Ada Lovelace", sample.OwnerName);
        Assert.Equal(new Domain.Sample.City(3000, new I18nText("Bern", "Bern")), sample.City);
        Assert.Equal(Clock.GetUtcNow(), sample.CreatedAt);
        var created = Assert.IsType<SampleEvent.Created>(Assert.Single(sample.PendingEvents));
        Assert.Equal(sample.Id, created.SampleId);
    }

    [Fact]
    public void Publish_moves_draft_to_published_and_registers_event()
    {
        var sample = Draft();

        sample.Publish(new SampleCommand.Publish());

        Assert.Equal(SampleState.Published, sample.State);
        Assert.IsType<SampleEvent.Published>(sample.PendingEvents[^1]);
    }

    [Fact]
    public void Publish_twice_is_not_allowed()
    {
        var sample = Draft();
        sample.Publish(new SampleCommand.Publish());

        var ex = Assert.Throws<OperationNotAllowedException>(() => sample.Publish(new SampleCommand.Publish()));

        Assert.Equal(typeof(SampleCommand.Publish), ex.CommandType);
        Assert.Equal("Published", ex.State);
    }

    [Fact]
    public void Archive_requires_published()
    {
        var sample = Draft();

        Assert.Throws<OperationNotAllowedException>(() => sample.Archive(new SampleCommand.Archive()));

        sample.Publish(new SampleCommand.Publish());
        sample.Archive(new SampleCommand.Archive());
        Assert.Equal(SampleState.Archived, sample.State);
    }

    [Fact]
    public void Update_replaces_city_copy_and_keeps_owner()
    {
        var sample = Draft(city: Bern);

        sample.Update(new SampleCommand.Update(new I18nText("New", "Neu"), "Changed", null), null);

        Assert.Equal(new I18nText("New", "Neu"), sample.Name);
        Assert.Null(sample.City);
        Assert.Equal("Ada Lovelace", sample.OwnerName);
        Assert.Equal("New", Assert.IsType<SampleEvent.Updated>(sample.PendingEvents[^1]).Name.En);
    }

    [Theory]
    [InlineData(SampleState.Draft, typeof(SampleCommand.Update), true)]
    [InlineData(SampleState.Draft, typeof(SampleCommand.Publish), true)]
    [InlineData(SampleState.Draft, typeof(SampleCommand.Archive), false)]
    [InlineData(SampleState.Published, typeof(SampleCommand.Publish), false)]
    [InlineData(SampleState.Published, typeof(SampleCommand.Archive), true)]
    [InlineData(SampleState.Archived, typeof(SampleCommand.Update), false)]
    [InlineData(SampleState.Archived, typeof(SampleCommand.UpdateOwnerName), true)]
    public void Can_reflects_the_state_machine(SampleState state, Type command, bool expected)
    {
        var sample = Draft();
        if (state is SampleState.Published or SampleState.Archived)
        {
            sample.Publish(new SampleCommand.Publish());
        }

        if (state is SampleState.Archived)
        {
            sample.Archive(new SampleCommand.Archive());
        }

        Assert.Equal(expected, sample.Can(command));
    }

    [Fact]
    public void Aggregates_are_equal_by_identity()
    {
        var a = Draft();
        var b = Draft();

        Assert.NotEqual(a, b);
        Assert.Equal(a, a);
        Assert.NotEqual(a.GetHashCode(), b.GetHashCode());
    }
}
