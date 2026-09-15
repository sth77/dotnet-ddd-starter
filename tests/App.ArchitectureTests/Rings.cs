using App.Api;
using App.Application.Sample;
using App.Domain.Common;
using App.Infrastructure.Persistence;
using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace App.ArchitectureTests;

/// <summary>The rings of the simplified onion, as ArchUnitNET object providers.</summary>
public static class Rings
{
    public static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(IIdentifier).Assembly,
            typeof(SampleOwnerNameSynchronizer).Assembly,
            typeof(AppDbContext).Assembly,
            typeof(ApiExtensions).Assembly,
            typeof(Program).Assembly)
        .Build();

    public static readonly IObjectProvider<IType> Domain =
        Types().That().ResideInAssembly(typeof(IIdentifier).Assembly).As("domain ring (App.Domain)");

    public static readonly IObjectProvider<IType> Application =
        Types().That().ResideInAssembly(typeof(SampleOwnerNameSynchronizer).Assembly).As("application ring (App.Application)");

    public static readonly IObjectProvider<IType> Infrastructure =
        Types().That().ResideInAssembly(typeof(AppDbContext).Assembly).As("infrastructure ring (App.Infrastructure)");

    public static readonly IObjectProvider<IType> Api =
        Types().That().ResideInAssembly(typeof(ApiExtensions).Assembly).As("infrastructure ring, HTTP surface (App.Api)");

    public static readonly IObjectProvider<IType> Host =
        Types().That().ResideInAssembly(typeof(Program).Assembly).As("composition root (App.Host)");
}
