using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence;

internal sealed class CityConfiguration : IEntityTypeConfiguration<Domain.ReferenceData.City>
{
    public void Configure(EntityTypeBuilder<Domain.ReferenceData.City> builder)
    {
        builder.ToTable("cities");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.PostalCode);
        builder.HasIndex(x => x.PostalCode);
        builder.ComplexProperty(x => x.Name);
    }
}
