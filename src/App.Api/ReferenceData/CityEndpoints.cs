using App.Api.Common;
using App.Domain.Common;
using App.Domain.ReferenceData;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace App.Api.ReferenceData;

public sealed record CityRepresentation(CityId Id, int PostalCode, I18nText Name) : HalResource;

public static class CityRels
{
    public const string Collection = "Cities";
    public const string Self = "City";
}

/// <summary>Reference data is read-only over HTTP; it changes through migrations/seeding, not through the API.</summary>
internal sealed class CityEndpoints : IEndpointModule
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cities").WithTags("Cities");

        group.MapGet("/", ListAsync).WithName(CityRels.Collection);
        group.MapGet("/{id}", GetAsync).WithName(CityRels.Self);
    }

    private static async Task<Ok<HalCollection<CityRepresentation>>> ListAsync(ICities cities, HalLinks links, HttpContext http, CancellationToken ct)
    {
        var items = new List<CityRepresentation>();
        foreach (var city in await cities.ListAsync(ct))
        {
            items.Add(await Represent(city, links, http));
        }

        return TypedResults.Ok(new HalCollection<CityRepresentation>("cities", items, links.Collection(http, CityRels.Collection)));
    }

    private static async Task<Results<Ok<CityRepresentation>, NotFound>> GetAsync(CityId id, ICities cities, HalLinks links, HttpContext http, CancellationToken ct)
    {
        var city = await cities.FindAsync(id, ct);
        return city is null ? TypedResults.NotFound() : TypedResults.Ok(await Represent(city, links, http));
    }

    private static async Task<CityRepresentation> Represent(City city, HalLinks links, HttpContext http)
        => new(city.Id, city.PostalCode, city.Name)
        {
            Links = await links.ForAsync(http, CityRels.Self, new { id = city.Id }, [], _ => false),
        };
}
