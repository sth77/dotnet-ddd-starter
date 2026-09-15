using ArchUnitNET.xUnitV3;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace App.ArchitectureTests;

/// <summary>
/// Feature-module boundaries inside App.Domain (design §3 option A: folder-per-feature, test-enforced).
/// When these rules get hard to keep, that is the trigger to split into project-per-feature (ADR-002).
/// </summary>
public sealed class ModuleRules
{
    private const string SampleModule = @"^App\.Domain\.Sample(\.|$)";
    private const string PersonModule = @"^App\.Domain\.Person(\.|$)";
    private const string ReferenceDataModule = @"^App\.Domain\.ReferenceData(\.|$)";

    [Fact]
    public void Person_module_does_not_know_the_sample_module()
        => Types().That().ResideInNamespaceMatching(PersonModule)
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(SampleModule)
            .Because("Sample depends on Person (owner); the reverse would be a cycle")
            .Check(Rings.Architecture);

    [Fact]
    public void Reference_data_knows_no_feature_module()
        => Types().That().ResideInNamespaceMatching(ReferenceDataModule)
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(SampleModule)
            .AndShould().NotDependOnAnyTypesThat().ResideInNamespaceMatching(PersonModule)
            .Because("reference data is copied from, never depends on the aggregates that copy it")
            .Check(Rings.Architecture);

    [Fact]
    public void Common_building_blocks_know_no_feature_module()
        => Types().That().ResideInNamespaceMatching(@"^App\.Domain\.Common(\.|$)")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(SampleModule)
            .AndShould().NotDependOnAnyTypesThat().ResideInNamespaceMatching(PersonModule)
            .AndShould().NotDependOnAnyTypesThat().ResideInNamespaceMatching(ReferenceDataModule)
            .Check(Rings.Architecture);
}
