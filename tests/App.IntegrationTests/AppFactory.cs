using App.Infrastructure.Messaging;
using App.Infrastructure.Persistence;
using App.Infrastructure.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace App.IntegrationTests;

/// <summary>
/// One PostgreSQL container and one host for the whole test collection. The host runs the real Program.cs with
/// the Testing environment, header authentication and migrate-on-startup (SQL scripts).
/// </summary>
public sealed class AppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        _ = Server; // build the host eagerly so startup validation fails the fixture, not a random test
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    public HttpClient CreateClientAs(string user, params string[] roles)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(HeaderAuthenticationHandler.UserHeader, user);
        client.DefaultRequestHeaders.Add(HeaderAuthenticationHandler.RolesHeader, string.Join(',', roles));
        return client;
    }

    /// <summary>
    /// The Modulith <c>Scenario</c> equivalent: waits until the outbox has delivered everything (or dead-lettered
    /// something, which fails the test with the recorded error).
    /// </summary>
    public async Task WaitForOutboxAsync(TimeSpan? timeout = null)
    {
        var deadline = TimeProvider.System.GetUtcNow() + (timeout ?? TimeSpan.FromSeconds(30));
        while (true)
        {
            await using var scope = Services.CreateAsyncScope();
            var outbox = scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<OutboxMessage>();

            var failed = await outbox.Where(m => m.FailedAt != null).Select(m => m.Type + ": " + m.LastError).FirstOrDefaultAsync();
            if (failed is not null)
            {
                throw new InvalidOperationException("Dead-lettered outbox message: " + failed);
            }

            if (!await outbox.AnyAsync(m => m.ProcessedAt == null))
            {
                return;
            }

            if (TimeProvider.System.GetUtcNow() > deadline)
            {
                throw new TimeoutException("Outbox not drained within " + timeout);
            }

            await Task.Delay(50);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(SecurityServiceCollectionExtensions.TestingEnvironment);
        builder.UseSetting("ConnectionStrings:App", _postgres.GetConnectionString());
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Authentication:Mode", nameof(AuthenticationMode.DevelopmentHeaders));
        builder.UseSetting("Outbox:PollInterval", "00:00:00.200");
    }
}

[CollectionDefinition(Name)]
public sealed class AppCollection : ICollectionFixture<AppFactory>
{
    public const string Name = "App";
}
