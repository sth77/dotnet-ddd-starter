using App.Api.Common;
using App.Domain.Common;
using App.Domain.Person;
using App.Domain.Sample;

namespace App.Api.Sample;

// Explicit response records replace Spring Data projections (design §6.3). Two shapes, two endpoints — no
// `?projection=` parameter.

public sealed record SampleSummary(
    SampleId Id,
    I18nText Name,
    SampleState State,
    string OwnerName) : HalResource;

public sealed record SampleDetail(
    SampleId Id,
    I18nText Name,
    string Description,
    Domain.Sample.City? City,
    SampleState State,
    PersonId Owner,
    string OwnerName,
    DateTimeOffset CreatedAt) : HalResource;

/// <summary>Endpoint names (stable keys for <c>LinkGenerator</c>) and the command → endpoint → rel binding.</summary>
public static class SampleRels
{
    public const string Collection = "Samples";
    public const string Self = "Sample";
    public const string Create = "CreateSample";
    public const string Update = "UpdateSample";
    public const string Publish = "PublishSample";
    public const string Archive = "ArchiveSample";

    /// <summary>
    /// Commands that have an endpoint. <c>SampleCommand.UpdateOwnerName</c> is deliberately absent: it is internal
    /// to the module and therefore never becomes a link.
    /// </summary>
    public static readonly IReadOnlyList<CommandEndpoint> CommandEndpoints =
    [
        new(typeof(SampleCommand.Update), "update", Update),
        new(typeof(SampleCommand.Publish), "publish", Publish),
        new(typeof(SampleCommand.Archive), "archive", Archive),
    ];
}
