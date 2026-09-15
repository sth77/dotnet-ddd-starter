using ArchUnitNET.xUnitV3;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace App.ArchitectureTests;

/// <summary>
/// Ring direction is already a project-reference fact (design §3); these rules document it and catch the
/// framework leaks that project references cannot express (design §8 layer 3).
/// </summary>
public sealed class RingRules
{
    [Fact]
    public void Domain_ring_depends_on_no_other_ring()
        => Types().That().Are(Rings.Domain)
            .Should().NotDependOnAny(Rings.Application)
            .AndShould().NotDependOnAny(Rings.Infrastructure)
            .AndShould().NotDependOnAny(Rings.Api)
            .AndShould().NotDependOnAny(Rings.Host)
            .Because("the domain is the innermost ring")
            .Check(Rings.Architecture);

    [Fact]
    public void Application_ring_depends_only_on_the_domain()
        => Types().That().Are(Rings.Application)
            .Should().NotDependOnAny(Rings.Infrastructure)
            .AndShould().NotDependOnAny(Rings.Api)
            .AndShould().NotDependOnAny(Rings.Host)
            .Check(Rings.Architecture);

    [Fact]
    public void Infrastructure_ring_does_not_depend_on_the_http_surface_or_the_host()
        => Types().That().Are(Rings.Infrastructure)
            .Should().NotDependOnAny(Rings.Api)
            .AndShould().NotDependOnAny(Rings.Host)
            .Check(Rings.Architecture);

    [Fact]
    public void Nothing_but_the_host_depends_on_the_host()
        => Types().That().Are(Rings.Api).Or().Are(Rings.Infrastructure).Or().Are(Rings.Application).Or().Are(Rings.Domain)
            .Should().NotDependOnAny(Rings.Host)
            .Check(Rings.Architecture);

    [Fact]
    public void Domain_and_application_rings_are_free_of_frameworks()
        => Types().That().Are(Rings.Domain).Or().Are(Rings.Application)
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^Microsoft\.EntityFrameworkCore")
            .AndShould().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^Microsoft\.AspNetCore")
            .AndShould().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^Microsoft\.Extensions")
            .AndShould().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^Npgsql")
            .Because("framework types belong to the infrastructure ring; the domain and application rings are plain C# (handlers included)")
            .Check(Rings.Architecture);

    [Fact]
    public void Messaging_infrastructure_is_confined_to_the_infrastructure_ring()
        => Types().That().Are(Rings.Api).Or().Are(Rings.Application).Or().Are(Rings.Domain)
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^App\.Infrastructure\.Messaging")
            .Because("handlers see IEventHandler<T> and IUnitOfWork only; the outbox is an infrastructure detail (ADR-008)")
            .Check(Rings.Architecture);

    [Fact]
    public void The_http_surface_never_saves_changes_directly()
        => Types().That().Are(Rings.Api)
            .Should().NotCallAny(MethodMembers().That().HaveNameStartingWith("SaveChanges"))
            .Because("reads may use the DbContext, state changes go through the aggregate and IUnitOfWork (design §6.3)")
            .Check(Rings.Architecture);
}
