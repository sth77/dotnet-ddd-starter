using App.Domain.Common;

namespace App.Domain.ReferenceData;

/// <summary>
/// Reference (master) data: an entity with its own lifecycle that aggregates <em>copy fields from</em>
/// rather than associate with, so an aggregate records the entry as it was at creation time (design §4.4).
/// </summary>
public sealed class Country : IEntity<CountryId>
{
    public Country(CountryId id, string code, I18nText name)
    {
        Id = id;
        Code = code;
        Name = name;
    }

#pragma warning disable CS8618 // Materialisation constructor: EF Core sets the remaining properties after construction.
    private Country(CountryId id, string code)
    {
        Id = id;
        Code = code;
    }
#pragma warning restore CS8618

    public CountryId Id { get; }

    /// <summary>Stable business key (for example an ISO code). Unique; safe to quote in an API contract.</summary>
    public string Code { get; }

    public I18nText Name { get; private set; }

    public void Rename(I18nText name) => Name = name;
}
