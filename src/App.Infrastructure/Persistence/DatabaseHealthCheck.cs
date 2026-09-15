using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace App.Infrastructure.Persistence;

/// <summary>Liveness of the application database (Actuator's db health indicator). No extra package needed.</summary>
public sealed class DatabaseHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        => await db.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Cannot connect to the application database.");
}
