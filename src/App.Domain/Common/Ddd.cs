using System.Diagnostics.CodeAnalysis;

namespace App.Domain.Common;

// The tactical DDD building blocks (design §4). Hand-rolled: ~100 lines, no dependency, and the
// interfaces carry the identifier type so analyzers, EF configuration and repositories can use it.

/// <summary>Marker for strongly-typed identifiers.</summary>
public interface IIdentifier;

/// <summary>
/// A value object that wraps exactly one primitive. The static <c>From</c> factory is the only way in, so
/// normalisation and validation live in one place; <see cref="Value"/> is the only way out. One generic JSON
/// converter and one generic EF converter serve every implementation (ADR-004).
/// </summary>
public interface IValueObject<TSelf, TPrimitive>
    where TSelf : IValueObject<TSelf, TPrimitive>
    where TPrimitive : notnull
{
    TPrimitive Value { get; }

    /// <exception cref="ValueObjectValidationException">When the primitive is not a valid value.</exception>
    static abstract TSelf From(TPrimitive value);
}

/// <summary>
/// A strongly-typed identifier: a value object over a primitive that is also parsable from a route segment.
/// Implemented as <c>readonly record struct</c>; analyzer DDD0002 forbids <c>new</c> and <c>default</c>.
/// </summary>
public interface IIdentifier<TSelf, TPrimitive> : IIdentifier, IValueObject<TSelf, TPrimitive>, IParsable<TSelf>
    where TSelf : struct, IIdentifier<TSelf, TPrimitive>
    where TPrimitive : notnull, IParsable<TPrimitive>;

/// <summary>Shared parsing for identifiers, so each identifier declares two one-line forwarders.</summary>
public static class Identifier
{
    public static TSelf Parse<TSelf, TPrimitive>(string s, IFormatProvider? provider)
        where TSelf : struct, IIdentifier<TSelf, TPrimitive>
        where TPrimitive : notnull, IParsable<TPrimitive>
        => TSelf.From(TPrimitive.Parse(s, provider));

    public static bool TryParse<TSelf, TPrimitive>([NotNullWhen(true)] string? s, IFormatProvider? provider, out TSelf result)
        where TSelf : struct, IIdentifier<TSelf, TPrimitive>
        where TPrimitive : notnull, IParsable<TPrimitive>
    {
        if (TPrimitive.TryParse(s, provider, out var primitive))
        {
            try
            {
                result = TSelf.From(primitive);
                return true;
            }
            catch (ValueObjectValidationException)
            {
                // fall through
            }
        }

        result = default;
        return false;
    }
}

/// <summary>An object with identity. Equality is identity equality.</summary>
public interface IEntity<out TId>
    where TId : IIdentifier
{
    TId Id { get; }
}

/// <summary>
/// The root of a consistency boundary. Other modules refer to it only through
/// <see cref="Association{TAggregate, TId}"/>, never by object reference.
/// </summary>
/// <typeparam name="TSelf">The implementing type (curiously recurring), so that the association type is exact.</typeparam>
/// <typeparam name="TId">The identifier type.</typeparam>
public interface IAggregateRoot<TSelf, out TId> : IEntity<TId>
    where TSelf : IAggregateRoot<TSelf, TId>
    where TId : IIdentifier;

/// <summary>Something that happened in the domain. Published after the owning aggregate is committed.</summary>
public interface IDomainEvent;

/// <summary>An intent to change an aggregate. Every state-changing aggregate operation takes exactly one.</summary>
public interface ICommand;

/// <summary>
/// Reference to an aggregate (possibly in another module) by identity only. This is the type that makes
/// "never hold an object reference across a module boundary" visible, and the one the architecture tests key on.
/// </summary>
public readonly record struct Association<TAggregate, TId>(TId Id)
    where TAggregate : IAggregateRoot<TAggregate, TId>
    where TId : IIdentifier
{
    public static Association<TAggregate, TId> To(TAggregate aggregate) => new(aggregate.Id);

    public override string ToString() => $"{typeof(TAggregate).Name}({Id})";
}

/// <summary>
/// Infrastructure-facing view of the events an aggregate has registered. Explicitly implemented by
/// <see cref="AggregateRoot{TSelf, TId}"/> so it does not clutter the aggregate's public surface.
/// </summary>
public interface IHasDomainEvents
{
    IReadOnlyList<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
