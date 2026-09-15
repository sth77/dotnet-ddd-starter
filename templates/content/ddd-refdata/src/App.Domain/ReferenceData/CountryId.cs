using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using App.Domain.Common;

namespace App.Domain.ReferenceData;

[JsonConverter(typeof(SingleValueJsonConverter<CountryId, Guid>))]
public readonly record struct CountryId : IIdentifier<CountryId, Guid>
{
    private CountryId(Guid value) => Value = value;

    public Guid Value { get; }

    public static CountryId New() => new(Guid.CreateVersion7());

    public static CountryId From(Guid value)
        => value == Guid.Empty ? throw new ValueObjectValidationException("CountryId must not be empty.") : new(value);

    public static CountryId Parse(string s, IFormatProvider? provider) => Identifier.Parse<CountryId, Guid>(s, provider);

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out CountryId result)
        => Identifier.TryParse<CountryId, Guid>(s, provider, out result);

    public override string ToString() => Value.ToString();
}
