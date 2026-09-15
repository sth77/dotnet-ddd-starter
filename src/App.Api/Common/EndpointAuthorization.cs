using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace App.Api.Common;

/// <summary>
/// Answers "may the current user invoke endpoint X?" by resolving the endpoint from <see cref="EndpointDataSource"/>,
/// reading its <see cref="IAuthorizeData"/> metadata and evaluating the combined policy. Decisions are cached per
/// request and endpoint name, so a collection response costs one evaluation per distinct policy (design §6.4 ⚠).
/// </summary>
internal sealed class EndpointAuthorization(
    EndpointDataSource endpoints,
    IAuthorizationPolicyProvider policyProvider,
    IAuthorizationService authorizationService)
{
    private static readonly object CacheKey = new();

    public async Task<bool> IsAuthorizedAsync(HttpContext http, string endpointName)
    {
        var cache = GetCache(http);
        if (cache.TryGetValue(endpointName, out var cached))
        {
            return cached;
        }

        var endpoint = endpoints.Endpoints.FirstOrDefault(e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == endpointName)
                       ?? throw new InvalidOperationException($"No endpoint named '{endpointName}' is registered.");

        var authorizeData = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
        bool allowed;
        if (authorizeData.Count == 0 || endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            allowed = true;
        }
        else
        {
            var policy = await AuthorizationPolicy.CombineAsync(policyProvider, authorizeData)
                         ?? throw new InvalidOperationException($"Endpoint '{endpointName}' has authorisation metadata but no policy could be built.");
            var result = await authorizationService.AuthorizeAsync(http.User, resource: null, policy);
            allowed = result.Succeeded;
        }

        cache[endpointName] = allowed;
        return allowed;
    }

    private static Dictionary<string, bool> GetCache(HttpContext http)
    {
        if (http.Items.TryGetValue(CacheKey, out var existing) && existing is Dictionary<string, bool> cache)
        {
            return cache;
        }

        var created = new Dictionary<string, bool>(StringComparer.Ordinal);
        http.Items[CacheKey] = created;
        return created;
    }
}
