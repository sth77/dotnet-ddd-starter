using App.Domain.Common;
using App.Domain.Todo;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Persistence;

/// <summary>
/// Repository implementation: module-private, three lines of LINQ where Spring Data derived a query.
/// <c>Set&lt;T&gt;()</c> rather than a <c>DbSet</c> property, so a new aggregate needs no edit to AppDbContext
/// (design §12, option A: no injection points).
/// </summary>
internal sealed class Todos(AppDbContext db) : ITodos
{
    public async Task<Domain.Todo.Todo?> FindAsync(TodoId id, CancellationToken ct = default)
        => await db.Set<Domain.Todo.Todo>().FindAsync([id], ct);

    public async Task<Domain.Todo.Todo> GetRequiredAsync(TodoId id, CancellationToken ct = default)
        => await FindAsync(id, ct) ?? throw new AggregateNotFoundException(typeof(Domain.Todo.Todo), id);

    public async Task<IReadOnlyList<Domain.Todo.Todo>> ListAsync(CancellationToken ct = default)
        => await db.Set<Domain.Todo.Todo>().OrderBy(x => x.CreatedAt).ToListAsync(ct);

    public void Add(Domain.Todo.Todo todo) => db.Set<Domain.Todo.Todo>().Add(todo);
}
