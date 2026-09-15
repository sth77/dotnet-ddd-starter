using App.Domain.Common;
using App.Domain.Person;

namespace App.Infrastructure.Persistence;

internal sealed class People(AppDbContext db) : IPeople
{
    public async Task<Domain.Person.Person?> FindAsync(PersonId id, CancellationToken ct = default)
        => await db.People.FindAsync([id], ct);

    public async Task<Domain.Person.Person> GetRequiredAsync(PersonId id, CancellationToken ct = default)
        => await FindAsync(id, ct) ?? throw new AggregateNotFoundException(typeof(Domain.Person.Person), id);

    public void Add(Domain.Person.Person person) => db.People.Add(person);
}
