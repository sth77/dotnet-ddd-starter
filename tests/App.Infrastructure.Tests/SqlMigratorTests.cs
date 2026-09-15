using App.Infrastructure.Persistence;

namespace App.Infrastructure.Tests;

/// <summary>Static properties of the migration scripts; applying them is covered by the integration suite.</summary>
public sealed class SqlMigratorTests
{
    [Fact]
    public void Scripts_are_embedded_versioned_and_ordered()
    {
        var scripts = SqlMigrator.Scripts;

        Assert.NotEmpty(scripts);
        Assert.All(scripts, s => Assert.Matches(@"^V\d{4}__[a-z0-9_]+$", s.Version));
        Assert.Equal(scripts.Select(s => s.Version).Order(StringComparer.Ordinal), scripts.Select(s => s.Version));
        Assert.Equal(scripts.Count, scripts.Select(s => s.Version[..5]).Distinct().Count());
        Assert.All(scripts, s => Assert.Equal(64, s.Checksum.Length));
    }
}
