using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence;

internal sealed class SampleConfiguration : IEntityTypeConfiguration<Domain.Sample.Sample>
{
    public void Configure(EntityTypeBuilder<Domain.Sample.Sample> builder)
    {
        builder.ToTable("samples");
        builder.HasKey(x => x.Id);

        builder.ComplexProperty(x => x.Name);                 // -> name_en, name_de
        builder.ComplexProperty(x => x.City, city =>         // -> city_postal_code, city_name_en, city_name_de
        {
            city.IsRequired(false);
            city.ComplexProperty(c => c.Name);
        });

        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.OwnerName).HasMaxLength(200);
        builder.Property(x => x.Owner);
        builder.HasIndex(x => x.Owner);
        builder.Property(x => x.State);
        builder.Property(x => x.CreatedAt);   // get-only properties are not discovered by convention

        builder.Ignore(x => x.PendingEvents);
    }
}
