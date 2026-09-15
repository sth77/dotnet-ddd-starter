using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

namespace App.Api.Common;

/// <summary>
/// Startup validation (design §6.4, §8 layer 4): every POST/PUT/PATCH/DELETE endpoint under <c>/api</c> must carry
/// authorisation metadata. Sees the real routing table rather than the source, and fails the host — and therefore
/// the integration test suite — before the first request.
/// </summary>
internal sealed class StateChangingEndpointsRequireAuthorization(EndpointDataSource endpoints) : IHostedService
{
    private static readonly string[] StateChangingMethods = ["POST", "PUT", "PATCH", "DELETE"];

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var offenders = endpoints.Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api", StringComparison.OrdinalIgnoreCase) == true)
            .Where(e => e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Any(m => StateChangingMethods.Contains(m, StringComparer.OrdinalIgnoreCase)) == true)
            .Where(e => e.Metadata.GetMetadata<IAuthorizeData>() is null || e.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(e => e.DisplayName ?? e.RoutePattern.RawText)
            .ToList();

        if (offenders.Count > 0)
        {
            throw new InvalidOperationException(
                "State-changing endpoints without an authorisation policy: " + string.Join(", ", offenders)
                + ". Add .RequireAuthorization(Policies.X) to the route.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
