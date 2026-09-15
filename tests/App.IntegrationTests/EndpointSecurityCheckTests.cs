using App.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;

namespace App.IntegrationTests;

/// <summary>Unit-level check of the startup validator; needs neither Docker nor the host.</summary>
public sealed class EndpointSecurityCheckTests
{
    [Fact]
    public async Task Unsecured_state_changing_endpoint_under_api_fails_startup()
    {
        var dataSource = new DefaultEndpointDataSource(
            Endpoint("/api/things", "POST"),
            Endpoint("/api/things/{id}", "GET"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new StateChangingEndpointsRequireAuthorization(dataSource).StartAsync(CancellationToken.None));

        Assert.Contains("/api/things", ex.Message);
        Assert.DoesNotContain("{id}", ex.Message);
    }

    [Fact]
    public async Task Secured_endpoints_and_reads_pass()
    {
        var dataSource = new DefaultEndpointDataSource(
            Endpoint("/api/things", "POST", new AuthorizeAttribute("User")),
            Endpoint("/api/things/{id}", "GET"),
            Endpoint("/health", "POST"));

        await new StateChangingEndpointsRequireAuthorization(dataSource).StartAsync(CancellationToken.None);
    }

    private static RouteEndpoint Endpoint(string pattern, string method, params object[] metadata)
    {
        var builder = new RouteEndpointBuilder(_ => Task.CompletedTask, RoutePatternFactory.Parse(pattern), 0)
        {
            DisplayName = $"{method} {pattern}",
        };
        builder.Metadata.Add(new HttpMethodMetadata([method]));
        foreach (var item in metadata)
        {
            builder.Metadata.Add(item);
        }

        return (RouteEndpoint)builder.Build();
    }
}
