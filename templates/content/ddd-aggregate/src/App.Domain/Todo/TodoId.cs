using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using App.Domain.Common;

namespace App.Domain.Todo;

[JsonConverter(typeof(SingleValueJsonConverter<TodoId, Guid>))]
public readonly record struct TodoId : IIdentifier<TodoId, Guid>
{
    private TodoId(Guid value) => Value = value;

    public Guid Value { get; }

    /// <summary>Sequential, index-friendly identifier (RFC 9562 UUID v7).</summary>
    public static TodoId New() => new(Guid.CreateVersion7());

    public static TodoId From(Guid value)
        => value == Guid.Empty ? throw new ValueObjectValidationException("TodoId must not be empty.") : new(value);

    public static TodoId Parse(string s, IFormatProvider? provider) => Identifier.Parse<TodoId, Guid>(s, provider);

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out TodoId result)
        => Identifier.TryParse<TodoId, Guid>(s, provider, out result);

    public override string ToString() => Value.ToString();
}
