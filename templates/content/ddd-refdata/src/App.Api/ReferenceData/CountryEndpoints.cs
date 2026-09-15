using App.Api.Common;
using App.Domain.Common;
using App.Domain.ReferenceData;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace App.Api.ReferenceData;

public sealed record CountryRepresentation(CountryId Id, string Code, I18nText Name) : HalResource;

public static class CountryRels
{
    public const string Collection = "Countries";
    public const string Self = "Country";
}

/// <summary>Reference data is read-only over HTTP; it changes through migrations/seeding, not through the API.</summary>
internal sealed class CountryEndpoints : IEndpointModule
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/countries").WithTags("Countries");

        group.MapGet("/", ListAsync).WithName(CountryRels.Collection);
        group.MapGet("/{id}", GetAsync).WithName(CountryRels.Self);
    }

    private static async Task<Ok<HalCollection<CountryRepresentation>>> ListAsync(ICountries countries, HalLinks links, HttpContext http, CancellationToken ct)
    {
        var items = new List<CountryRepresentation>();
        foreach (var country in await countries.ListAsync(ct))
        {
            items.Add(await Represent(country, links, http));
        }

        return TypedResults.Ok(new HalCollection<CountryRepresentation>("countries", items, links.Collection(http, CountryRels.Collection)));
    }

    private static async Task<Results<Ok<CountryRepresentation>, NotFound>> GetAsync(CountryId id, ICountries countries, HalLinks links, HttpContext http, CancellationToken ct)
    {
        var country = await countries.FindAsync(id, ct);
        return country is null ? TypedResults.NotFound() : TypedResults.Ok(await Represent(country, links, http));
    }

    private static async Task<CountryRepresentation> Represent(Country country, HalLinks links, HttpContext http)
        => new(country.Id, country.Code, country.Name)
        {
            Links = await links.ForAsync(http, CountryRels.Self, new { id = country.Id }, [], _ => false),
        };
}
