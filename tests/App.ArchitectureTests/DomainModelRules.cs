using System.Reflection;
using App.Domain.Common;

namespace App.ArchitectureTests;

/// <summary>
/// Rules about the shape of the domain model, checked by reflection where the question is about members rather
/// than dependencies (the jMolecules DDD rule set, hand-rolled — design §9).
/// </summary>
public sealed class DomainModelRules
{
    private static readonly Assembly Domain = typeof(IIdentifier).Assembly;

    private static IEnumerable<Type> AggregateRoots => Domain.GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false } && ImplementsGeneric(t, typeof(IAggregateRoot<,>)));

    [Fact]
    public void Aggregates_reference_other_aggregates_only_through_associations()
    {
        var offenders = AggregateRoots
            .SelectMany(aggregate => aggregate.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(p => Unwrap(p.PropertyType).Any(t => ImplementsGeneric(t, typeof(IAggregateRoot<,>)) || ImplementsGeneric(t, typeof(IEntity<>))))
                .Select(p => $"{aggregate.Name}.{p.Name}"))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Repositories_never_expose_IQueryable()
    {
        var offenders = Domain.GetTypes()
            .Where(t => t.IsInterface)
            .SelectMany(i => i.GetMethods())
            .Where(m => Unwrap(m.ReturnType).Any(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IQueryable<>) || t == typeof(IQueryable)))
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Commands_and_events_form_closed_hierarchies_of_sealed_records()
    {
        var members = Domain.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && (typeof(ICommand).IsAssignableFrom(t) || typeof(IDomainEvent).IsAssignableFrom(t)))
            .ToList();

        Assert.NotEmpty(members);
        foreach (var member in members)
        {
            Assert.True(member.IsSealed, $"{member.Name} must be sealed");
            Assert.True(IsRecord(member), $"{member.Name} must be a record (immutable, value-equal)");
            Assert.True(member.IsNested, $"{member.Name} must be nested in its aggregate's command/event hierarchy");

            var parent = member.DeclaringType!;
            Assert.True(parent.IsAbstract, $"{parent.Name} must be abstract");
            Assert.True(
                parent.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(c => !IsRecordCopyConstructor(c))
                    .All(c => c.IsPrivate),
                $"{parent.Name} must have only private constructors so the hierarchy is closed");
        }
    }

    [Fact]
    public void Every_aggregate_root_derives_from_the_base_class_and_has_a_Can_predicate()
    {
        foreach (var aggregate in AggregateRoots)
        {
            var baseType = aggregate.BaseType;
            Assert.True(
                baseType is { IsGenericType: true } && baseType.GetGenericTypeDefinition() == typeof(AggregateRoot<,>),
                $"{aggregate.Name} must derive from AggregateRoot<TSelf, TId>");

            Assert.NotNull(aggregate.GetMethod("Can", [typeof(Type)]));
        }
    }

    [Fact]
    public void Identifiers_are_record_structs_with_private_constructors_and_a_json_converter()
    {
        var identifiers = Domain.GetTypes()
            .Where(t => typeof(IIdentifier).IsAssignableFrom(t) && t is { IsInterface: false, IsGenericTypeDefinition: false })
            .ToList();

        Assert.NotEmpty(identifiers);
        foreach (var identifier in identifiers)
        {
            Assert.True(identifier.IsValueType, $"{identifier.Name} must be a struct");
            Assert.True(IsRecord(identifier), $"{identifier.Name} must be a record struct (value equality, ToString)");
            Assert.True(
                identifier.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IIdentifier<,>)),
                $"{identifier.Name} must implement IIdentifier<TSelf, TPrimitive>");
            Assert.Empty(identifier.GetConstructors());
            Assert.NotEmpty(identifier.GetCustomAttributes(typeof(System.Text.Json.Serialization.JsonConverterAttribute), false));
        }
    }

    [Fact]
    public void Aggregates_do_not_expose_public_setters()
    {
        var offenders = AggregateRoots
            .SelectMany(a => a.GetProperties().Where(p => p.SetMethod is { IsPublic: true }).Select(p => $"{a.Name}.{p.Name}"))
            .ToList();

        Assert.Empty(offenders);
    }

    private static bool ImplementsGeneric(Type type, Type openGeneric)
        => type.GetInterfaces().Concat([type]).Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == openGeneric);

    // Record classes get a <Clone>$ method, record structs do not; both get a compiler-generated PrintMembers.
    private static bool IsRecord(Type type)
        => type.GetMethod("<Clone>$") is not null
           || type.GetMethod("PrintMembers", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, [typeof(System.Text.StringBuilder)]) is not null;

    // The compiler emits a protected copy constructor for every record; it cannot open the hierarchy.
    private static bool IsRecordCopyConstructor(ConstructorInfo constructor)
        => constructor.GetParameters() is [{ ParameterType: var t }] && t == constructor.DeclaringType;

    private static IEnumerable<Type> Unwrap(Type type)
    {
        yield return type;

        // Association<TAggregate, TId> names the aggregate type on purpose: that is the sanctioned reference.
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Association<,>))
        {
            yield break;
        }

        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments().SelectMany(Unwrap))
            {
                yield return argument;
            }
        }
    }
}
