using App.Domain.Common;
using Microsoft.EntityFrameworkCore;
using City = App.Domain.ReferenceData.City;
using Person = App.Domain.Person.Person;
using Sample = App.Domain.Sample.Sample;

namespace App.Infrastructure.Persistence;

/// <summary>
/// Single DbContext for the modular monolith. Mapping lives in <see cref="IEntityTypeConfiguration{TEntity}"/>
/// classes next to it; the domain model carries no persistence attributes (design §5.1). The schema itself
/// comes from SQL scripts (SqlMigrator); SchemaValidationTests keeps model and schema aligned.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Sample> Samples => Set<Sample>();

    public DbSet<Person> People => Set<Person>();

    public DbSet<City> Cities => Set<City>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Every enum in the model is stored as its name (design §5.3). Verified by EnumConventionTests.
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(50);

        // Identifiers and single-value value objects collapse to their primitive column; associations are stored
        // as the target's primitive id (design §5.2). Discovered from the domain assembly, nothing to register per type.
        configurationBuilder.RegisterDomainValueObjects();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Aggregates, reference data, and the outbox/inbox tables (App.Infrastructure.Messaging) — every
        // IEntityTypeConfiguration<T> in this assembly. Adding an aggregate never touches this class.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Optimistic concurrency via PostgreSQL's system column, once for every aggregate root (design §4.2).
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.ClrType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAggregateRoot<,>)))
            {
                // Equivalent of Npgsql's UseXminAsConcurrencyToken() for the non-generic builder.
                modelBuilder.Entity(entityType.ClrType)
                    .Property<uint>("xmin")
                    .HasColumnType("xid")
                    .ValueGeneratedOnAddOrUpdate()
                    .IsConcurrencyToken();
            }
        }
    }
}
