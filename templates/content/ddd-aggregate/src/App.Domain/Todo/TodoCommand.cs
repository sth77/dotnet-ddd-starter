using System.ComponentModel.DataAnnotations;
using App.Domain.Common;

namespace App.Domain.Todo;

/// <summary>
/// Closed hierarchy of commands for <see cref="Todo"/>: one record per operation. The private constructor
/// means only nested types can derive, which is the C# spelling of a sealed interface.
/// </summary>
public abstract record TodoCommand : ICommand
{
    private TodoCommand()
    {
    }

    public sealed record Create(
        [property: Required, MaxLength(200)] string Name,
        [property: Required, MaxLength(1000)] string Description) : TodoCommand;

    public sealed record Update(
        [property: Required, MaxLength(200)] string Name,
        [property: Required, MaxLength(1000)] string Description) : TodoCommand;

    public sealed record Activate : TodoCommand;

    public sealed record Close : TodoCommand;
}
