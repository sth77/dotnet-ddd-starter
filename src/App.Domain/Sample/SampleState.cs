namespace App.Domain.Sample;

/// <summary>Lifecycle of a <see cref="Sample"/>. Persisted as a string (see AppDbContext.ConfigureConventions).</summary>
public enum SampleState
{
    Draft,
    Published,
    Archived,
}
