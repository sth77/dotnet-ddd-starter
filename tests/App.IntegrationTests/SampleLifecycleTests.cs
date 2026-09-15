using System.Net;
using System.Net.Http.Json;
using App.Infrastructure.Persistence;

namespace App.IntegrationTests;

[Collection(AppCollection.Name)]
public sealed class SampleLifecycleTests(AppFactory factory)
{
    [Fact]
    public async Task Create_returns_201_with_location_and_copies_owner_and_city()
    {
        var user = factory.User();
        var (owner, _) = await Api.CreatePersonAsync(user, "Grace Hopper");

        var response = await user.PostAsJsonAsync("/api/samples", new
        {
            name = new { en = "Compiler", de = "Compiler" },
            description = "First",
            city = SeededCities.Bern.Value,
            owner,
        }, Api.Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Body();
        Assert.Equal($"/api/samples/{body["id"]}", response.Headers.Location!.ToString());
        Assert.Equal("Draft", body["state"]!.GetValue<string>());
        Assert.Equal("Grace Hopper", body["ownerName"]!.GetValue<string>());
        Assert.Equal(3000, body["city"]!["postalCode"]!.GetValue<int>());
        Assert.Equal("Bern", body["city"]!["name"]!["de"]!.GetValue<string>());
    }

    [Fact]
    public async Task Links_reflect_state_and_the_callers_authorisation()
    {
        var user = factory.User();
        var admin = factory.Admin();
        var (owner, _) = await Api.CreatePersonAsync(user);
        var (id, created) = await Api.CreateSampleAsync(user, owner);

        // Draft, as user: publish needs Admin, so only update is offered.
        Assert.Equal(["self", "update"], created.LinkRels().Keys.Order());

        // Draft, as admin: publish appears.
        var asAdmin = await (await admin.GetAsync($"/api/samples/{id}")).Body();
        Assert.Equal(["publish", "self", "update"], asAdmin.LinkRels().Keys.Order());
        Assert.Equal($"/api/samples/{id}/publish", asAdmin.LinkRels()["publish"]);

        // Anonymous read works, and offers no command links at all.
        var anonymous = await (await factory.CreateClient().GetAsync($"/api/samples/{id}")).Body();
        Assert.Equal(["self"], anonymous.LinkRels().Keys);
    }

    [Fact]
    public async Task Publish_is_admin_only_and_not_repeatable()
    {
        var user = factory.User();
        var admin = factory.Admin();
        var (owner, _) = await Api.CreatePersonAsync(user);
        var (id, _) = await Api.CreateSampleAsync(user, owner);

        Assert.Equal(HttpStatusCode.Forbidden, (await user.PostAsync($"/api/samples/{id}/publish", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().PostAsync($"/api/samples/{id}/publish", null)).StatusCode);

        var published = await admin.PostAsync($"/api/samples/{id}/publish", null);
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        var body = await published.Body();
        Assert.Equal("Published", body["state"]!.GetValue<string>());
        Assert.Equal(["archive", "self", "update"], body.LinkRels().Keys.Order());

        var again = await admin.PostAsync($"/api/samples/{id}/publish", null);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        var problem = await again.Body();
        Assert.Equal("urn:problem-type:operation-not-allowed", problem["type"]!.GetValue<string>());
        Assert.Equal("Publish", problem["command"]!.GetValue<string>());
        Assert.Equal("Published", problem["state"]!.GetValue<string>());

        var archived = await (await admin.PostAsync($"/api/samples/{id}/archive", null)).Body();
        Assert.Equal("Archived", archived["state"]!.GetValue<string>());
        Assert.Equal(["self"], archived.LinkRels().Keys);
    }

    [Fact]
    public async Task Missing_aggregates_are_404_problems()
    {
        var admin = factory.Admin();

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/samples/{Guid.CreateVersion7()}")).StatusCode);

        var publish = await admin.PostAsync($"/api/samples/{Guid.CreateVersion7()}/publish", null);
        Assert.Equal(HttpStatusCode.NotFound, publish.StatusCode);
        Assert.Equal("urn:problem-type:not-found", (await publish.Body())["type"]!.GetValue<string>());
    }

    [Fact]
    public async Task Invalid_commands_are_400_validation_problems_before_the_handler_runs()
    {
        var user = factory.User();
        var (owner, _) = await Api.CreatePersonAsync(user);

        var response = await user.PostAsJsonAsync("/api/samples", new
        {
            name = new { en = "", de = "x" },
            description = new string('x', 1001),
            owner,
        }, Api.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Body())["errors"]!.AsObject().Select(e => e.Key).ToList();
        Assert.Contains(errors, k => k.Contains("Description", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, k => k.Contains("En", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Invalid_value_objects_in_the_body_are_422_problems()
    {
        var user = factory.User();

        var response = await user.PostAsJsonAsync("/api/people", new { name = "No Mail", email = "not-an-email" }, Api.Json);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("urn:problem-type:invalid-value", (await response.Body())["type"]!.GetValue<string>());
    }

    [Fact]
    public async Task Collection_is_a_HAL_collection_with_per_item_links()
    {
        var user = factory.User();
        var (owner, _) = await Api.CreatePersonAsync(user);
        var (id, _) = await Api.CreateSampleAsync(user, owner);

        var body = await (await user.GetAsync("/api/samples")).Body();

        Assert.Equal("/api/samples", body.LinkRels()["self"]);
        var item = body["_embedded"]!["samples"]!.AsArray().Single(s => s!["id"]!.GetValue<Guid>() == id)!;
        Assert.Equal($"/api/samples/{id}", item.LinkRels()["self"]);
        Assert.Equal("A sample", item["name"]!["en"]!.GetValue<string>());
    }
}
