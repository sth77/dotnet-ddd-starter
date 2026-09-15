using App.Domain.Common;
using App.Domain.Person;

namespace App.Domain.Sample;

/// <summary>
/// Repository for <see cref="Sample"/>. Plural of the aggregate, domain language, no suffix. Never exposes
/// <c>IQueryable</c>: read models take their own path through the API ring (design §4.3, §6.3).
/// </summary>
public interface ISamples
{
    Task<Sample?> FindAsync(SampleId id, CancellationToken ct = default);

    /// <exception cref="AggregateNotFoundException">When no sample with that id exists.</exception>
    Task<Sample> GetRequiredAsync(SampleId id, CancellationToken ct = default);

    Task<IReadOnlyList<Sample>> FindByOwnerAsync(Association<Person.Person, PersonId> owner, CancellationToken ct = default);

    void Add(Sample sample);
}
