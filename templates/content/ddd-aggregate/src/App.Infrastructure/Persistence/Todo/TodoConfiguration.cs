using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence;

internal sealed class TodoConfiguration : IEntityTypeConfiguration<Domain.Todo.Todo>
{
    public void Configure(EntityTypeBuilder<Domain.Todo.Todo> builder)
    {
        builder.ToTable("todos");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.State);
        builder.Property(x => x.CreatedAt);   // get-only properties are not discovered by convention

        builder.Ignore(x => x.PendingEvents);
    }
}
