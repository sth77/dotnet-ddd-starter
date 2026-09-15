using System.ComponentModel.DataAnnotations;
using App.Domain.Common;
using App.Domain.Person;
using App.Domain.ReferenceData;

namespace App.Domain.Sample;

/// <summary>
/// Closed hierarchy of commands for <see cref="Sample"/>: one record per operation. The private constructor
/// means only nested types can derive, which is the C# spelling of a sealed interface.
/// </summary>
public abstract record SampleCommand : ICommand
{
    private SampleCommand()
    {
    }

    public sealed record Create(
        [property: Required] I18nText Name,
        [property: Required, MaxLength(1000)] string Description,
        CityId? City,
        PersonId Owner) : SampleCommand;

    public sealed record Update(
        [property: Required] I18nText Name,
        [property: Required, MaxLength(1000)] string Description,
        CityId? City) : SampleCommand;

    public sealed record Publish : SampleCommand;

    public sealed record Archive : SampleCommand;

    /// <summary>Internal: raised by the owner-name synchroniser. No endpoint handles it, so it never becomes a HAL link.</summary>
    public sealed record UpdateOwnerName([property: Required, MaxLength(200)] string OwnerName) : SampleCommand;
}
