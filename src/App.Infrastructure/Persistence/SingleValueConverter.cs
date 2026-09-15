using App.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace App.Infrastructure.Persistence;

/// <summary>Stores any <see cref="IValueObject{TSelf, TPrimitive}"/> as its primitive column.</summary>
internal sealed class SingleValueConverter<TSelf, TPrimitive>()
    : ValueConverter<TSelf, TPrimitive>(value => value.Value, primitive => FromPrimitive(primitive))
    where TSelf : IValueObject<TSelf, TPrimitive>
    where TPrimitive : notnull
{
    // Expression trees cannot call static abstract interface members directly (CS8927); a helper method can.
    private static TSelf FromPrimitive(TPrimitive primitive) => TSelf.From(primitive);
}

/// <summary>Stores an <see cref="Association{TAggregate, TId}"/> as the referenced aggregate's primitive id.</summary>
internal sealed class AssociationConverter<TAggregate, TId, TPrimitive>()
    : ValueConverter<Association<TAggregate, TId>, TPrimitive>(
        association => association.Id.Value,
        primitive => new Association<TAggregate, TId>(FromPrimitive(primitive)))
    where TAggregate : IAggregateRoot<TAggregate, TId>
    where TId : struct, IIdentifier<TId, TPrimitive>
    where TPrimitive : notnull, IParsable<TPrimitive>
{
    private static TId FromPrimitive(TPrimitive primitive) => TId.From(primitive);
}

internal static class ValueObjectConventions
{
    /// <summary>
    /// Registers a converter for every single-value value object and every association type found in the domain
    /// assembly, so adding an identifier needs no persistence code at all (ADR-004).
    /// </summary>
    public static void RegisterDomainValueObjects(this ModelConfigurationBuilder builder)
    {
        var domain = typeof(IIdentifier).Assembly;

        foreach (var type in domain.GetTypes().Where(t => t is { IsAbstract: false, IsGenericTypeDefinition: false }))
        {
            var valueObject = type.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValueObject<,>));
            if (valueObject is null)
            {
                continue;
            }

            var primitive = valueObject.GetGenericArguments()[1];
            builder.Properties(type).HaveConversion(typeof(SingleValueConverter<,>).MakeGenericType(type, primitive));
        }

        foreach (var aggregate in domain.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false }))
        {
            var root = aggregate.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAggregateRoot<,>));
            if (root is null)
            {
                continue;
            }

            var id = root.GetGenericArguments()[1];
            var primitive = id.GetInterfaces()
                .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IIdentifier<,>))
                .GetGenericArguments()[1];

            builder.Properties(typeof(Association<,>).MakeGenericType(aggregate, id))
                .HaveConversion(typeof(AssociationConverter<,,>).MakeGenericType(aggregate, id, primitive));
        }
    }
}
