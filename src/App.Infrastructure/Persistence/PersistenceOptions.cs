using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Persistence;

/// <summary>The one place the provider and naming convention are chosen. Used at runtime and by the model tests.</summary>
public static class PersistenceOptions
{
    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder options, string connectionString)
        => options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention();
}
