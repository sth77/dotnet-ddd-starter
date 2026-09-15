using System.Diagnostics.CodeAnalysis;
using App.Domain.Common;

namespace App.Domain.Person;

/// <summary>A person who can own samples. Second module, so that cross-module rules have something to bite on.</summary>
public sealed class Person : AggregateRoot<Person, PersonId>
{
    private Person(PersonId id, string name, EmailAddress email)
        : base(id)
    {
        Name = name;
        Email = email;
    }

    public string Name { get; private set; }

    public EmailAddress Email { get; private set; }

    public static Person Create(PersonCommand.Create data)
    {
        var person = new Person(PersonId.New(), data.Name, data.Email);
        person.RegisterEvent(new PersonEvent.Created(person.Id, person.Name));
        return person;
    }

    public void Update(PersonCommand.Update data)
    {
        var nameChanged = !string.Equals(Name, data.Name, StringComparison.Ordinal);

        Name = data.Name;
        Email = data.Email;

        if (nameChanged)
        {
            RegisterEvent(new PersonEvent.Updated(Id, Name));
        }
    }

    public bool Can<TCommand>()
        where TCommand : PersonCommand
        => Can(typeof(TCommand));

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Uniform aggregate API; HAL link generation calls Can on an instance.")]
    public bool Can(Type command) => command == typeof(PersonCommand.Update);
}
