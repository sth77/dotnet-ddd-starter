using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using App.Domain.Common;

namespace App.Domain.Person;

[JsonConverter(typeof(SingleValueJsonConverter<PersonId, Guid>))]
public readonly record struct PersonId : IIdentifier<PersonId, Guid>
{
    private PersonId(Guid value) => Value = value;

    public Guid Value { get; }

    public static PersonId New() => new(Guid.CreateVersion7());

    public static PersonId From(Guid value)
        => value == Guid.Empty ? throw new ValueObjectValidationException("PersonId must not be empty.") : new(value);

    public static PersonId Parse(string s, IFormatProvider? provider) => Identifier.Parse<PersonId, Guid>(s, provider);

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out PersonId result)
        => Identifier.TryParse<PersonId, Guid>(s, provider, out result);

    public override string ToString() => Value.ToString();
}
