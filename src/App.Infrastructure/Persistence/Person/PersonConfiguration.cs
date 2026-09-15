using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence;

internal sealed class PersonConfiguration : IEntityTypeConfiguration<Domain.Person.Person>
{
    public void Configure(EntityTypeBuilder<Domain.Person.Person> builder)
    {
        builder.ToTable("people");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(200);
        builder.Property(x => x.Email).HasMaxLength(320);
        builder.HasIndex(x => x.Email).IsUnique();

        builder.Ignore(x => x.PendingEvents);
    }
}
