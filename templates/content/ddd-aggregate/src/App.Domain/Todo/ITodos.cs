namespace App.Domain.Todo;

/// <summary>
/// Repository for <see cref="Todo"/>. Plural of the aggregate, domain language, no suffix. Never exposes
/// <c>IQueryable</c>: read models take their own path through the API ring (design §4.3, §6.3).
/// </summary>
public interface ITodos
{
    Task<Todo?> FindAsync(TodoId id, CancellationToken ct = default);

    /// <exception cref="Common.AggregateNotFoundException">When no entry with that id exists.</exception>
    Task<Todo> GetRequiredAsync(TodoId id, CancellationToken ct = default);

    Task<IReadOnlyList<Todo>> ListAsync(CancellationToken ct = default);

    void Add(Todo todo);
}
