using System.Text.Json.Serialization;
using App.Api.Common;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace App.Api;

public static class ApiExtensions
{
    /// <summary>
    /// Validation, ProblemDetails, HAL services, authorisation policies and the endpoint-security startup check.
    /// <c>AddValidation()</c> is called here, in the same compilation as the endpoints, because its source
    /// generator discovers validatable types from the <c>Map*</c> calls it can see.
    /// </summary>
    public static IServiceCollection AddApi(this IServiceCollection services)
    {
        services.AddValidation();
        services.AddProblemDetails();
        services.AddExceptionHandler<DomainExceptionHandler>();

        services.Configure<JsonOptions>(options =>
        {
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.User, policy => policy.RequireRole(Roles.User, Roles.Admin))
            .AddPolicy(Policies.Admin, policy => policy.RequireRole(Roles.Admin));

        services.AddScoped<EndpointAuthorization>();
        services.AddScoped<HalLinks>();
        services.AddHostedService<StateChangingEndpointsRequireAuthorization>();

        return services;
    }

    /// <summary>Maps every <see cref="IEndpointModule"/> in this assembly. No registration list to maintain.</summary>
    public static IEndpointRouteBuilder MapApi(this IEndpointRouteBuilder app)
    {
        var modules = typeof(ApiExtensions).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IEndpointModule).IsAssignableFrom(t))
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .Select(t => (IEndpointModule)Activator.CreateInstance(t)!);

        foreach (var module in modules)
        {
            module.Map(app);
        }

        return app;
    }
}
