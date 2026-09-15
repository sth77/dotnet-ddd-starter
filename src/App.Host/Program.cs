using App.Api;
using App.Host;
using App.Infrastructure;
using App.Infrastructure.Persistence;
using App.Infrastructure.Security;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("App")
                       ?? throw new InvalidOperationException("ConnectionStrings:App is not configured.");

// Rings, outermost first: infrastructure, then the HTTP surface.
builder.Services.AddPersistence(connectionString);
builder.Services.AddMessaging(builder.Configuration);
builder.Services.AddSecurity(builder.Configuration, builder.Environment);
builder.Services.AddApi();

builder.Services.AddOpenApi(options => options.AddSchemaTransformer<SingleValueSchemaTransformer>());
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

var app = builder.Build();

// One-shot migration job (deployments): `dotnet App.Host.dll --migrate` applies pending SQL scripts and exits.
if (args.Contains("--migrate", StringComparer.Ordinal))
{
    var applied = await app.Services.GetRequiredService<SqlMigrator>().MigrateAsync();
    if (app.Logger.IsEnabled(LogLevel.Information))
    {
        app.Logger.LogInformation("Applied {Count} migration(s): {Versions}", applied.Count, string.Join(", ", applied));
    }

    return;
}

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    // Convenience for the inner loop and the test host, not for production replicas.
    await app.Services.GetRequiredService<SqlMigrator>().MigrateAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

app.MapApi();
app.MapOpenApi();
app.MapScalarApiReference(options => options.WithTitle("dotnet-ddd-starter"));
app.MapHealthChecks("/health");

await app.RunAsync();

/// <summary>Exposes the entry point to <c>WebApplicationFactory&lt;Program&gt;</c>. Top-level statements put Program in the global namespace.</summary>
public partial class Program;
