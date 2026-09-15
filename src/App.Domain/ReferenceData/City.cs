using App.Domain.Common;

namespace App.Domain.ReferenceData;

/// <summary>
/// Reference (master) data: an entity with its own lifecycle that aggregates <em>copy fields from</em>
/// rather than associate with, so an aggregate records the city as it was at creation time (design §4.4).
/// </summary>
public sealed class City : IEntity<CityId>
{
    public City(CityId id, int postalCode, I18nText name)
    {
        Id = id;
        PostalCode = postalCode;
        Name = name;
    }

#pragma warning disable CS8618 // Materialisation constructor: EF Core sets the remaining properties after construction.
    private City(CityId id, int postalCode)
    {
        Id = id;
        PostalCode = postalCode;
    }
#pragma warning restore CS8618

    public CityId Id { get; }

    public int PostalCode { get; }

    public I18nText Name { get; private set; }

    public void Rename(I18nText name) => Name = name;
}
