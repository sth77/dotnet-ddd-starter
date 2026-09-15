using System.ComponentModel.DataAnnotations;
using App.Domain.Common;

namespace App.Domain.Person;

public abstract record PersonCommand : ICommand
{
    private PersonCommand()
    {
    }

    public sealed record Create(
        [property: Required, MaxLength(200)] string Name,
        EmailAddress Email) : PersonCommand;

    public sealed record Update(
        [property: Required, MaxLength(200)] string Name,
        EmailAddress Email) : PersonCommand;
}
