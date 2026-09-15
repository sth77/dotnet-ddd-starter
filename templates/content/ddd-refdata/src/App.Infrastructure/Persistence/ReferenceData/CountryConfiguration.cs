using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Persistence;

internal sealed class CountryConfiguration : IEntityTypeConfiguration<Domain.ReferenceData.Country>
{
    public void Configure(EntityTypeBuilder<Domain.ReferenceData.Country> builder)
    {
        builder.ToTable("countries");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).HasMaxLength(10);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.ComplexProperty(x => x.Name);
    }
}
