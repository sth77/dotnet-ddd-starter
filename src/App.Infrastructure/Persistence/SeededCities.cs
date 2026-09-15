using App.Domain.ReferenceData;

namespace App.Infrastructure.Persistence;

/// <summary>Stable identifiers of the reference data seeded by <c>Migrations/V0002__seed_reference_data.sql</c>.</summary>
public static class SeededCities
{
    public static readonly CityId Lausanne = CityId.From(new Guid("00000000-0000-7000-8000-000000001000"));
    public static readonly CityId Bern = CityId.From(new Guid("00000000-0000-7000-8000-000000003000"));
    public static readonly CityId Zurich = CityId.From(new Guid("00000000-0000-7000-8000-000000008000"));
}
