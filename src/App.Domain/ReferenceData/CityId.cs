using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using App.Domain.Common;

namespace App.Domain.ReferenceData;

[JsonConverter(typeof(SingleValueJsonConverter<CityId, Guid>))]
public readonly record struct CityId : IIdentifier<CityId, Guid>
{
    private CityId(Guid value) => Value = value;

    public Guid Value { get; }

    public static CityId New() => new(Guid.CreateVersion7());

    public static CityId From(Guid value)
        => value == Guid.Empty ? throw new ValueObjectValidationException("CityId must not be empty.") : new(value);

    public static CityId Parse(string s, IFormatProvider? provider) => Identifier.Parse<CityId, Guid>(s, provider);

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out CityId result)
        => Identifier.TryParse<CityId, Guid>(s, provider, out result);

    public override string ToString() => Value.ToString();
}
