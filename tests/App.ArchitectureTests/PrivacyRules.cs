using System.Reflection;
using App.Api;
using App.Domain.Common;
using App.Infrastructure.Persistence;

namespace App.ArchitectureTests;

/// <summary>
/// Compiler-enforced module privacy (design §10.1): implementations stay <c>internal</c>; only the domain
/// interfaces and the representations are public API.
/// </summary>
public sealed class PrivacyRules
{
    [Fact]
    public void Repository_implementations_are_internal()
    {
        var repositoryInterfaces = typeof(IIdentifier).Assembly.GetTypes()
            .Where(t => t.IsInterface && t.Namespace != "App.Domain.Common" && t.Name.StartsWith('I'))
            .ToHashSet();

        var offenders = typeof(AppDbContext).Assembly.GetTypes()
            .Where(t => t.IsClass && t.GetInterfaces().Any(repositoryInterfaces.Contains))
            .Where(t => t.IsPublic)
            .Select(t => t.FullName)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Endpoint_modules_are_internal_and_sealed()
    {
        var moduleInterface = typeof(ApiExtensions).Assembly.GetType("App.Api.Common.IEndpointModule")!;

        var modules = typeof(ApiExtensions).Assembly.GetTypes()
            .Where(t => t.IsClass && moduleInterface.IsAssignableFrom(t))
            .ToList();

        Assert.NotEmpty(modules);
        Assert.Empty(modules.Where(m => m.IsPublic || !m.IsSealed).Select(m => m.FullName));
        Assert.All(modules, m => Assert.NotNull(m.GetConstructor(BindingFlags.Instance | BindingFlags.Public, Type.EmptyTypes)));
    }
}
