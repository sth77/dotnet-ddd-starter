using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using App.Domain.Common;

namespace App.Domain.Person;

/// <summary>
/// Single-value, validated value object with plain .NET: a sealed record with a private constructor, so the only
/// way to obtain one is <see cref="From"/>. Collapses to one column and to a bare JSON string.
/// </summary>
[JsonConverter(typeof(SingleValueJsonConverter<EmailAddress, string>))]
public sealed record EmailAddress : IValueObject<EmailAddress, string>
{
    private EmailAddress(string value) => Value = value;

    public string Value { get; }

    public static EmailAddress From(string value)
        => TryFrom(value, out var email) ? email : throw new ValueObjectValidationException("Must be a valid e-mail address.");

    public static bool TryFrom(string? input, [NotNullWhen(true)] out EmailAddress? result)
    {
        var normalised = input?.Trim().ToLowerInvariant() ?? string.Empty;
        var at = normalised.IndexOf('@', StringComparison.Ordinal);

        if (at > 0 && at < normalised.Length - 1 && normalised.Length <= 320)
        {
            result = new EmailAddress(normalised);
            return true;
        }

        result = null;
        return false;
    }

    public override string ToString() => Value;
}
