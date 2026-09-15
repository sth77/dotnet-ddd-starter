using App.Domain.Common;
using App.Domain.ReferenceData;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Persistence;

internal sealed class Cities(AppDbContext db) : ICities
{
    public async Task<City?> FindAsync(CityId id, CancellationToken ct = default)
        => await db.Cities.FindAsync([id], ct);

    public async Task<City> GetRequiredAsync(CityId id, CancellationToken ct = default)
        => await FindAsync(id, ct) ?? throw new AggregateNotFoundException(typeof(City), id);

    public async Task<IReadOnlyList<City>> ListAsync(CancellationToken ct = default)
        => await db.Cities.AsNoTracking().OrderBy(c => c.PostalCode).ToListAsync(ct);
}
