namespace App.Domain.Todo;

/// <summary>Lifecycle of a <see cref="Todo"/>. Persisted as a string (see AppDbContext.ConfigureConventions).</summary>
public enum TodoState
{
    Draft,
    Active,
    Closed,
}
