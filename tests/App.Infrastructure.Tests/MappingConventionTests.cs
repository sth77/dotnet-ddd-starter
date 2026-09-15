using App.Domain.Person;
using App.Domain.Sample;
using App.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using City = App.Domain.ReferenceData.City;

namespace App.Infrastructure.Tests;

/// <summary>
/// Pins the mapping conventions the design relies on (§5.2, §5.3, §4.2) so a refactor cannot silently change
/// the physical schema. These are the three ⚠ items from the design document, confirmed against the model.
/// The schema itself is checked against the database by SchemaValidationTests in the integration suite.
/// </summary>
public sealed class MappingConventionTests
{
    [Fact]
    public void Enums_are_stored_as_strings_by_the_single_convention()
    {
        using var context = ModelFixture.CreateContext();

        var state = context.Model.FindEntityType(typeof(Domain.Sample.Sample))!.FindProperty(nameof(Domain.Sample.Sample.State))!;

        Assert.Equal(typeof(string), ProviderType(state));
        Assert.Equal("character varying(50)", state.GetColumnType());
    }

    [Fact]
    public void Identifiers_and_single_value_objects_collapse_to_their_primitive_column()
    {
        using var context = ModelFixture.CreateContext();

        var sampleId = context.Model.FindEntityType(typeof(Domain.Sample.Sample))!.FindProperty(nameof(Domain.Sample.Sample.Id))!;
        var email = context.Model.FindEntityType(typeof(Domain.Person.Person))!.FindProperty(nameof(Domain.Person.Person.Email))!;

        Assert.Equal(typeof(SampleId), sampleId.ClrType);
        Assert.Equal(typeof(Guid), ProviderType(sampleId));
        Assert.Equal("id", sampleId.GetColumnName());
        Assert.Equal(typeof(string), ProviderType(email));
        Assert.Equal("email", email.GetColumnName());
    }

    [Fact]
    public void Associations_are_stored_as_the_target_id()
    {
        using var context = ModelFixture.CreateContext();

        var owner = context.Model.FindEntityType(typeof(Domain.Sample.Sample))!.FindProperty(nameof(Domain.Sample.Sample.Owner))!;

        Assert.Equal(typeof(Guid), ProviderType(owner));
        Assert.Equal("owner", owner.GetColumnName());
    }

    [Fact]
    public void Multi_field_value_objects_are_complex_types_prefixed_with_the_owning_property()
    {
        using var context = ModelFixture.CreateContext();
        var sample = context.Model.FindEntityType(typeof(Domain.Sample.Sample))!;
        var table = StoreObjectIdentifier.Table("samples");

        var name = sample.FindComplexProperty(nameof(Domain.Sample.Sample.Name))!;
        Assert.Equal(["name_de", "name_en"], name.ComplexType.GetProperties().Select(p => p.GetColumnName(table)).Order());

        var city = sample.FindComplexProperty(nameof(Domain.Sample.Sample.City))!;
        Assert.True(city.IsNullable, "City is an optional complex type (EF 10)");
        Assert.Equal(["city_postal_code"], city.ComplexType.GetProperties().Select(p => p.GetColumnName(table)));
        Assert.Equal(
            ["city_name_de", "city_name_en"],
            city.ComplexType.GetComplexProperties().Single().ComplexType.GetProperties().Select(p => p.GetColumnName(table)).Order());
    }

    [Fact]
    public void Every_aggregate_root_uses_xmin_as_concurrency_token_and_reference_data_does_not()
    {
        using var context = ModelFixture.CreateContext();

        foreach (var aggregate in new[] { typeof(Domain.Sample.Sample), typeof(Domain.Person.Person) })
        {
            var xmin = context.Model.FindEntityType(aggregate)!.FindProperty("xmin");
            Assert.NotNull(xmin);
            Assert.True(xmin.IsConcurrencyToken);
            Assert.Equal(ValueGenerated.OnAddOrUpdate, xmin.ValueGenerated);
        }

        Assert.Null(context.Model.FindEntityType(typeof(City))!.FindProperty("xmin"));
    }

    [Fact]
    public void Physical_names_are_snake_case()
    {
        using var context = ModelFixture.CreateContext();

        Assert.Equal("samples", context.Model.FindEntityType(typeof(Domain.Sample.Sample))!.GetTableName());
        Assert.Equal("owner_name", context.Model.FindEntityType(typeof(Domain.Sample.Sample))!.FindProperty(nameof(Domain.Sample.Sample.OwnerName))!.GetColumnName());
        Assert.Equal("people", context.Model.FindEntityType(typeof(Domain.Person.Person))!.GetTableName());
        Assert.Equal("postal_code", context.Model.FindEntityType(typeof(City))!.FindProperty(nameof(City.PostalCode))!.GetColumnName());
    }

    [Fact]
    public void Outbox_and_inbox_are_part_of_the_model()
    {
        using var context = ModelFixture.CreateContext();

        Assert.Equal("outbox_messages", context.Model.FindEntityType(typeof(OutboxMessage))!.GetTableName());
        Assert.Equal("jsonb", context.Model.FindEntityType(typeof(OutboxMessage))!.FindProperty(nameof(OutboxMessage.Payload))!.GetColumnType());
        Assert.Equal(
            ["handler", "message_id"],
            context.Model.FindEntityType(typeof(InboxMessage))!.FindPrimaryKey()!.Properties.Select(p => p.GetColumnName()).Order());
    }

    [Fact]
    public void Person_id_is_not_confused_with_sample_id()
    {
        using var context = ModelFixture.CreateContext();

        Assert.Equal(typeof(PersonId), context.Model.FindEntityType(typeof(Domain.Person.Person))!.FindPrimaryKey()!.Properties.Single().ClrType);
    }

    private static Type? ProviderType(IProperty property)
        => property.GetProviderClrType() ?? property.GetValueConverter()?.ProviderClrType ?? property.GetTypeMapping().Converter?.ProviderClrType;
}
