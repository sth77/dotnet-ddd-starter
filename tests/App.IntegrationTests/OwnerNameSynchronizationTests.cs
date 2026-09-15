using System.Net.Http.Json;
using App.Infrastructure.Messaging;
using App.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace App.IntegrationTests;

[Collection(AppCollection.Name)]
public sealed class OwnerNameSynchronizationTests(AppFactory factory)
{
    [Fact]
    public async Task Renaming_the_owner_updates_the_denormalised_owner_name_through_the_outbox()
    {
        var user = factory.User();
        var (owner, _) = await Api.CreatePersonAsync(user, "Ada");
        var (id, created) = await Api.CreateSampleAsync(user, owner);
        Assert.Equal("Ada", created["ownerName"]!.GetValue<string>());

        var response = await user.PutAsJsonAsync($"/api/people/{owner}", new { name = "Ada Lovelace", email = "ada@example.org" }, Api.Json);
        response.EnsureSuccessStatusCode();

        await factory.WaitForOutboxAsync();

        var sample = await (await user.GetAsync($"/api/samples/{id}")).Body();
        Assert.Equal("Ada Lovelace", sample["ownerName"]!.GetValue<string>());

        // Idempotent consumption is recorded per handler, in the handler's own transaction.
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updated = (await db.Set<OutboxMessage>().Where(m => m.Type == "App.Domain.Person.PersonEvent+Updated").ToListAsync())
            .Single(m => m.Payload.Contains(owner.ToString(), StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(updated.ProcessedAt);
        Assert.Equal(1, updated.Attempts);
        Assert.True(await db.Set<InboxMessage>().AnyAsync(i => i.MessageId == updated.Id && i.Handler.EndsWith("SampleOwnerNameSynchronizer")));
    }
}
