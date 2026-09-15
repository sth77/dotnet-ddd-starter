namespace App.Domain.ReferenceData;

public interface ICountries
{
    Task<Country?> FindAsync(CountryId id, CancellationToken ct = default);

    /// <exception cref="Common.AggregateNotFoundException">When no entry with that id exists.</exception>
    Task<Country> GetRequiredAsync(CountryId id, CancellationToken ct = default);

    Task<IReadOnlyList<Country>> ListAsync(CancellationToken ct = default);
}
