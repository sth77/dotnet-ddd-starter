using App.Domain.Common;
using App.Domain.Person;
using App.Domain.Sample;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Persistence;

/// <summary>Repository implementation: module-private, three lines of LINQ where Spring Data derived a query.</summary>
internal sealed class Samples(AppDbContext db) : ISamples
{
    public async Task<Domain.Sample.Sample?> FindAsync(SampleId id, CancellationToken ct = default)
        => await db.Samples.FindAsync([id], ct);

    public async Task<Domain.Sample.Sample> GetRequiredAsync(SampleId id, CancellationToken ct = default)
        => await FindAsync(id, ct) ?? throw new AggregateNotFoundException(typeof(Domain.Sample.Sample), id);

    public async Task<IReadOnlyList<Domain.Sample.Sample>> FindByOwnerAsync(Association<Domain.Person.Person, PersonId> owner, CancellationToken ct = default)
        => await db.Samples.Where(s => s.Owner == owner).OrderBy(s => s.CreatedAt).ToListAsync(ct);

    public void Add(Domain.Sample.Sample sample) => db.Samples.Add(sample);
}
