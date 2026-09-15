using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using App.Api.Common;

namespace App.IntegrationTests;

/// <summary>Small helpers for the HTTP contract: JSON bodies and HAL link sets.</summary>
internal static class Api
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<JsonNode> Body(this HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<JsonNode>(Json))!;

    public static IReadOnlyDictionary<string, string> LinkRels(this JsonNode body)
        => body["_links"]!.AsObject().ToDictionary(p => p.Key, p => p.Value!["href"]!.GetValue<string>(), StringComparer.Ordinal);

    public static async Task<(Guid Id, JsonNode Body)> CreatePersonAsync(HttpClient client, string name = "Ada Lovelace", string? email = null)
    {
        var response = await client.PostAsJsonAsync("/api/people", new { name, email = email ?? $"{Guid.CreateVersion7():N}@example.org" }, Json);
        response.EnsureSuccessStatusCode();
        var body = await response.Body();
        return (body["id"]!.GetValue<Guid>(), body);
    }

    public static async Task<(Guid Id, JsonNode Body)> CreateSampleAsync(HttpClient client, Guid owner, Guid? city = null)
    {
        var response = await client.PostAsJsonAsync("/api/samples", new
        {
            name = new { en = "A sample", de = "Ein Beispiel" },
            description = "Created by the integration tests",
            city,
            owner,
        }, Json);
        response.EnsureSuccessStatusCode();
        var body = await response.Body();
        return (body["id"]!.GetValue<Guid>(), body);
    }

    public static HttpClient User(this AppFactory factory) => factory.CreateClientAs("alice", Roles.User);

    public static HttpClient Admin(this AppFactory factory) => factory.CreateClientAs("root", Roles.User, Roles.Admin);
}
