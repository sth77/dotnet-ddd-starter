using App.Domain.Common;

namespace App.Domain.Person;

public interface IPeople
{
    Task<Person?> FindAsync(PersonId id, CancellationToken ct = default);

    /// <exception cref="AggregateNotFoundException">When no person with that id exists.</exception>
    Task<Person> GetRequiredAsync(PersonId id, CancellationToken ct = default);

    void Add(Person person);
}
