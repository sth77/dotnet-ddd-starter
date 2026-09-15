using App.Domain.Common;
using App.Domain.Person;

namespace App.Domain.Tests;

public sealed class PersonTests
{
    [Fact]
    public void Update_with_new_name_registers_updated_event()
    {
        var person = Domain.Person.Person.Create(new PersonCommand.Create("Ada", EmailAddress.From("ada@example.org")));

        person.Update(new PersonCommand.Update("Ada Lovelace", EmailAddress.From("ada@example.org")));

        Assert.Equal("Ada Lovelace", Assert.IsType<PersonEvent.Updated>(person.PendingEvents[^1]).Name);
    }

    [Fact]
    public void Update_with_same_name_registers_no_event()
    {
        var person = Domain.Person.Person.Create(new PersonCommand.Create("Ada", EmailAddress.From("ada@example.org")));

        person.Update(new PersonCommand.Update("Ada", EmailAddress.From("ada.lovelace@example.org")));

        Assert.Equal("ada.lovelace@example.org", person.Email.Value);
        Assert.IsType<PersonEvent.Created>(Assert.Single(person.PendingEvents));
    }

    [Fact]
    public void Email_is_normalised_and_validated()
    {
        Assert.Equal("ada@example.org", EmailAddress.From("  Ada@Example.org ").Value);

        Assert.Throws<ValueObjectValidationException>(() => EmailAddress.From("not-an-email"));
        Assert.False(EmailAddress.TryFrom("@x", out _));
        Assert.Equal(EmailAddress.From("a@b.c"), EmailAddress.From("A@B.C"));
    }
}
