using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace App.Api.Common;

/// <summary>
/// Builds the link set for an aggregate: <c>self</c>, plus one link per command that the aggregate permits in
/// its current state <em>and</em> the current user may invoke. Authorisation is read from the live routing
/// table's endpoint metadata (design §6.4), so it cannot drift from the policy attached to the route.
/// </summary>
internal sealed class HalLinks(LinkGenerator linkGenerator, EndpointAuthorization authorization)
{
    public async Task<IReadOnlyDictionary<string, HalLink>> ForAsync(
        HttpContext http,
        string selfEndpointName,
        object routeValues,
        IReadOnlyList<CommandEndpoint> commands,
        Func<Type, bool> can)
    {
        var links = new Dictionary<string, HalLink>(StringComparer.Ordinal)
        {
            [Rels.Self] = Link(http, selfEndpointName, routeValues),
        };

        foreach (var command in commands)
        {
            if (!can(command.CommandType))
            {
                continue;
            }

            if (!await authorization.IsAuthorizedAsync(http, command.EndpointName))
            {
                continue;
            }

            links[command.Rel] = Link(http, command.EndpointName, routeValues);
        }

        return links;
    }

    public IReadOnlyDictionary<string, HalLink> Collection(HttpContext http, string endpointName)
        => new Dictionary<string, HalLink>(StringComparer.Ordinal) { [Rels.Self] = Link(http, endpointName, null) };

    private HalLink Link(HttpContext http, string endpointName, object? routeValues)
    {
        // Paths, not absolute URIs: robust behind reverse proxies without forwarded-header configuration.
        var path = linkGenerator.GetPathByName(http, endpointName, routeValues)
                   ?? throw new InvalidOperationException($"No endpoint named '{endpointName}' is registered.");
        return new HalLink(path);
    }
}
