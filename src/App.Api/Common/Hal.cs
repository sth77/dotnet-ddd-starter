using System.Text.Json.Serialization;

namespace App.Api.Common;

// Capability discovery for API clients (design §6.4, §16.1). HAL is the encoding; the abstraction — a link
// per permitted command — is what matters, and it is confined to this file plus HalLinks so it can be swapped.

/// <summary>A HAL link object.</summary>
public sealed record HalLink(string Href, string? Title = null);

/// <summary>Base for single-resource representations carrying <c>_links</c>.</summary>
public abstract record HalResource
{
    [JsonPropertyName("_links")]
    [JsonPropertyOrder(1000)]
    public required IReadOnlyDictionary<string, HalLink> Links { get; init; }
}

/// <summary>HAL collection: items under <c>_embedded.{rel}</c>, links under <c>_links</c>.</summary>
public sealed record HalCollection<T>
{
    public HalCollection(string rel, IReadOnlyList<T> items, IReadOnlyDictionary<string, HalLink> links)
    {
        Embedded = new Dictionary<string, IReadOnlyList<T>> { [rel] = items };
        Links = links;
    }

    [JsonPropertyName("_embedded")]
    public IReadOnlyDictionary<string, IReadOnlyList<T>> Embedded { get; }

    [JsonPropertyName("_links")]
    public IReadOnlyDictionary<string, HalLink> Links { get; }
}

/// <summary>Standard IANA link relations used by the starter.</summary>
public static class Rels
{
    public const string Self = "self";
    public const string Collection = "collection";
}

/// <summary>Binds a command type to the endpoint that handles it and the link relation it is exposed under.</summary>
public sealed record CommandEndpoint(Type CommandType, string Rel, string EndpointName);
