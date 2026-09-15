using System.Text.Json;
using App.Domain.Common;

namespace App.Infrastructure.Messaging;

/// <summary>Maps stored type names to the domain event CLR types, without ever calling <c>Type.GetType</c> on data.</summary>
public static class DomainEventTypes
{
    public static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.General);

    private static readonly Dictionary<string, Type> ByName = typeof(IDomainEvent).Assembly
        .GetTypes()
        .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IDomainEvent).IsAssignableFrom(t))
        .ToDictionary(NameOf, t => t, StringComparer.Ordinal);

    public static string NameOf(Type eventType) => eventType.FullName!;

    public static Type Resolve(string name)
        => ByName.TryGetValue(name, out var type)
            ? type
            : throw new InvalidOperationException($"'{name}' is not a domain event type of this application. Was it renamed with messages still in the outbox?");
}
