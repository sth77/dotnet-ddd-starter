using App.Domain.Common;
using App.Domain.ReferenceData;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Persistence;

/// <summary>
/// Repository implementation: module-private. <c>Set&lt;T&gt;()</c> rather than a <c>DbSet</c> property, so a new
/// reference-data entity needs no edit to AppDbContext (design §12, option A: no injection points).
/// </summary>
internal sealed class Countries(AppDbContext db) : ICountries
{
    public async Task<Country?> FindAsync(CountryId id, CancellationToken ct = default)
        => await db.Set<Country>().FindAsync([id], ct);

    public async Task<Country> GetRequiredAsync(CountryId id, CancellationToken ct = default)
        => await FindAsync(id, ct) ?? throw new AggregateNotFoundException(typeof(Country), id);

    public async Task<IReadOnlyList<Country>> ListAsync(CancellationToken ct = default)
        => await db.Set<Country>().AsNoTracking().OrderBy(x => x.Code).ToListAsync(ct);
}
