using System.Net;
using System.Text.Json.Nodes;

namespace App.IntegrationTests;

[Collection(AppCollection.Name)]
public sealed class ContractTests(AppFactory factory)
{
    [Fact]
    public async Task OpenApi_document_describes_value_objects_as_their_primitive()
    {
        var response = await factory.CreateClient().GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Body();
        var schemas = document["components"]!["schemas"]!.AsObject();

        var sampleId = schemas["SampleId"]!;
        Assert.Equal("string", sampleId["type"]!.GetValue<string>());
        Assert.Equal("uuid", sampleId["format"]!.GetValue<string>());
        Assert.Null(sampleId["properties"]);

        Assert.Contains("/api/samples/{id}/publish", document["paths"]!.AsObject().Select(p => p.Key));
    }

    [Fact]
    public async Task Health_endpoint_reports_healthy()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Reference_data_is_seeded_by_migration_and_read_only()
    {
        var client = factory.CreateClient();

        var cities = await (await client.GetAsync("/api/cities")).Body();
        Assert.Equal([1000, 3000, 8000], cities["_embedded"]!["cities"]!.AsArray().Select(c => c!["postalCode"]!.GetValue<int>()));

        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.PostAsync("/api/cities", null)).StatusCode);
    }

    /// <summary>
    /// Golden master of the link contract per state × role (design §10.8). A silent change here is exactly what
    /// this test exists to catch; accept a deliberate one by replacing Snapshots/HalLinkContract.json.
    /// </summary>
    [Fact]
    public async Task Hal_link_contract_snapshot()
    {
        var user = factory.User();
        var admin = factory.Admin();
        var (owner, _) = await Api.CreatePersonAsync(user);
        var (id, _) = await Api.CreateSampleAsync(user, owner);

        // Sequential on purpose: every step changes the state the next one observes.
        var snapshot = new JsonObject();
        snapshot["draft/user"] = Links(await (await user.GetAsync($"/api/samples/{id}")).Body(), id);
        snapshot["draft/admin"] = Links(await (await admin.GetAsync($"/api/samples/{id}")).Body(), id);
        (await admin.PostAsync($"/api/samples/{id}/publish", null)).EnsureSuccessStatusCode();
        snapshot["published/user"] = Links(await (await user.GetAsync($"/api/samples/{id}")).Body(), id);
        snapshot["published/admin"] = Links(await (await admin.GetAsync($"/api/samples/{id}")).Body(), id);
        (await admin.PostAsync($"/api/samples/{id}/archive", null)).EnsureSuccessStatusCode();
        snapshot["archived/user"] = Links(await (await user.GetAsync($"/api/samples/{id}")).Body(), id);
        snapshot["archived/admin"] = Links(await (await admin.GetAsync($"/api/samples/{id}")).Body(), id);

        Snapshot.Match("HalLinkContract", snapshot.ToJsonString(new() { WriteIndented = true }));
    }

    private static JsonObject Links(JsonNode body, Guid id)
    {
        var links = new JsonObject();
        foreach (var (rel, href) in body.LinkRels().OrderBy(l => l.Key, StringComparer.Ordinal))
        {
            links[rel] = href.Replace(id.ToString(), "{id}", StringComparison.Ordinal);
        }

        return links;
    }
}
