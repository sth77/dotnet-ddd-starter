using System.Text.Json;
using System.Text.Json.Serialization;

namespace App.Domain.Common;

/// <summary>
/// Serialises any <see cref="IValueObject{TSelf, TPrimitive}"/> as its bare primitive (<c>"0199…"</c>, not
/// <c>{"value":"0199…"}</c>). Applied per type with <c>[JsonConverter(typeof(SingleValueJsonConverter&lt;X, Guid&gt;))]</c>
/// so it also works for plain <c>JsonSerializer.Serialize(x)</c> calls without options.
/// </summary>
public sealed class SingleValueJsonConverter<TSelf, TPrimitive> : JsonConverter<TSelf>
    where TSelf : IValueObject<TSelf, TPrimitive>
    where TPrimitive : notnull
{
    public override TSelf Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var primitive = JsonSerializer.Deserialize<TPrimitive>(ref reader, options)
                        ?? throw new JsonException($"{typeof(TSelf).Name} cannot be null.");
        return TSelf.From(primitive);
    }

    public override void Write(Utf8JsonWriter writer, TSelf value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value.Value, options);

    public override TSelf ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => TSelf.From(JsonSerializer.Deserialize<TPrimitive>($"\"{reader.GetString()}\"", options)!);

    public override void WriteAsPropertyName(Utf8JsonWriter writer, TSelf value, JsonSerializerOptions options)
        => writer.WritePropertyName(value.Value.ToString()!);
}
