using System.Text.Json;
using App.Domain.Common;
using App.Domain.Person;
using App.Domain.Sample;

namespace App.Domain.Tests;

public sealed class AssociationTests
{
    [Fact]
    public void Associations_are_value_equal_by_id()
    {
        var id = PersonId.New();

        Assert.Equal(new Association<Domain.Person.Person, PersonId>(id), new Association<Domain.Person.Person, PersonId>(id));
        Assert.NotEqual(new Association<Domain.Person.Person, PersonId>(id), new Association<Domain.Person.Person, PersonId>(PersonId.New()));
    }

    [Fact]
    public void Identifiers_serialise_as_bare_values()
    {
        var id = SampleId.From(new Guid("0199d6a5-0000-7000-8000-000000000001"));

        Assert.Equal("\"0199d6a5-0000-7000-8000-000000000001\"", JsonSerializer.Serialize(id));
        Assert.Equal(id, JsonSerializer.Deserialize<SampleId>("\"0199d6a5-0000-7000-8000-000000000001\""));
        Assert.Equal("\"ada@example.org\"", JsonSerializer.Serialize(EmailAddress.From("ada@example.org")));
    }

    [Fact]
    public void Identifiers_are_sequential_uuid_v7_and_never_empty()
    {
        Assert.Equal(7, SampleId.New().Value.Version);
        Assert.Throws<ValueObjectValidationException>(() => SampleId.From(Guid.Empty));
    }

    [Fact]
    public void Identifiers_parse_from_route_segments()
    {
        var id = SampleId.New();

        Assert.True(SampleId.TryParse(id.ToString(), null, out var parsed));
        Assert.Equal(id, parsed);
        Assert.False(SampleId.TryParse("not-a-guid", null, out _));
        Assert.False(SampleId.TryParse(Guid.Empty.ToString(), null, out _));
    }
}
