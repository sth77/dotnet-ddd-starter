using App.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Tests;

/// <summary>Builds the EF model with the real provider but never opens a connection.</summary>
public static class ModelFixture
{
    public static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        PersistenceOptions.Configure(options, "Host=localhost;Database=model-only;Username=x;Password=y");
        return new AppDbContext(options.Options);
    }
}
