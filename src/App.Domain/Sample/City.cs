using App.Domain.Common;

namespace App.Domain.Sample;

/// <summary>
/// The city as it was when the sample was created or last updated: a <em>copy</em> of the reference-data
/// entry, not an association to it (design §4.4). Mapped as an optional EF complex type.
/// </summary>
public sealed record City(int PostalCode, I18nText Name)
{
    public static City From(ReferenceData.City city) => new(city.PostalCode, city.Name);

    // Materialisation constructor: EF Core cannot constructor-bind a nested complex property (Name), so it
    // needs a parameterless path and sets the properties afterwards.
    private City()
        : this(0, null!)
    {
    }
}
