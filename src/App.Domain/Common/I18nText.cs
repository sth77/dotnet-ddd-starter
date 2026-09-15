using System.ComponentModel.DataAnnotations;

namespace App.Domain.Common;

/// <summary>
/// A text in the application's supported languages. Multi-field value object: a plain record,
/// mapped as an EF complex type (columns <c>{owner}_en</c>, <c>{owner}_de</c>).
/// </summary>
public sealed record I18nText(
    [property: Required, MaxLength(200)] string En,
    [property: Required, MaxLength(200)] string De)
{
    public string In(string languageCode) => languageCode.ToUpperInvariant() switch
    {
        "DE" => De,
        _ => En,
    };
}
