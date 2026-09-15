namespace App.Domain.ReferenceData;

public interface ICities
{
    Task<City?> FindAsync(CityId id, CancellationToken ct = default);

    /// <exception cref="Common.AggregateNotFoundException">When no city with that id exists.</exception>
    Task<City> GetRequiredAsync(CityId id, CancellationToken ct = default);

    Task<IReadOnlyList<City>> ListAsync(CancellationToken ct = default);
}
