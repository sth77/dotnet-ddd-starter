using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using App.Domain.Common;

namespace App.Domain.Sample;

[JsonConverter(typeof(SingleValueJsonConverter<SampleId, Guid>))]
public readonly record struct SampleId : IIdentifier<SampleId, Guid>
{
    private SampleId(Guid value) => Value = value;

    public Guid Value { get; }

    /// <summary>Sequential, index-friendly identifier (RFC 9562 UUID v7).</summary>
    public static SampleId New() => new(Guid.CreateVersion7());

    public static SampleId From(Guid value)
        => value == Guid.Empty ? throw new ValueObjectValidationException("SampleId must not be empty.") : new(value);

    public static SampleId Parse(string s, IFormatProvider? provider) => Identifier.Parse<SampleId, Guid>(s, provider);

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out SampleId result)
        => Identifier.TryParse<SampleId, Guid>(s, provider, out result);

    public override string ToString() => Value.ToString();
}
