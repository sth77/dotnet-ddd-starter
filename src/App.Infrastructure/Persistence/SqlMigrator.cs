using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace App.Infrastructure.Persistence;

/// <summary>
/// SQL-first schema migrations (ADR-007): hand-written, reviewable scripts embedded as <c>Migrations/V0001__name.sql</c>,
/// applied in order, each in its own transaction, recorded in <c>schema_version</c> with a checksum. An applied
/// script that was edited fails the run — add a new script instead. A PostgreSQL advisory lock serialises
/// concurrent starters. Roughly what Flyway does, in 100 lines, with no dependency.
/// </summary>
public sealed partial class SqlMigrator(string connectionString, TimeProvider clock, ILogger<SqlMigrator> logger)
{
    private const string ResourcePrefix = "Migrations/";
    private const long LockKey = 7_463_920_118; // arbitrary, application-wide

    public static IReadOnlyList<MigrationScript> Scripts { get; } = LoadScripts();

    /// <returns>The names of the scripts applied in this run.</returns>
    public async Task<IReadOnlyList<string>> MigrateAsync(CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        await ExecuteAsync(connection, $"SELECT pg_advisory_lock({LockKey})", ct);
        try
        {
            await ExecuteAsync(connection, """
                CREATE TABLE IF NOT EXISTS schema_version (
                    version    text        NOT NULL PRIMARY KEY,
                    checksum   text        NOT NULL,
                    applied_at timestamptz NOT NULL)
                """, ct);

            var applied = await ReadAppliedAsync(connection, ct);
            var appliedNow = new List<string>();

            foreach (var script in Scripts)
            {
                if (applied.TryGetValue(script.Version, out var checksum))
                {
                    if (!string.Equals(checksum, script.Checksum, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"Migration {script.Version} was modified after it was applied (checksum {checksum} → {script.Checksum}). "
                            + "Never edit an applied migration; add a new one.");
                    }

                    continue;
                }

                await using var transaction = await connection.BeginTransactionAsync(ct);
                await ExecuteAsync(connection, script.Sql, ct);
                await using (var record = new NpgsqlCommand("INSERT INTO schema_version (version, checksum, applied_at) VALUES (@v, @c, @t)", connection))
                {
                    record.Parameters.AddWithValue("v", script.Version);
                    record.Parameters.AddWithValue("c", script.Checksum);
                    record.Parameters.AddWithValue("t", clock.GetUtcNow());
                    await record.ExecuteNonQueryAsync(ct);
                }

                await transaction.CommitAsync(ct);
                appliedNow.Add(script.Version);
                LogApplied(script.Version);
            }

            return appliedNow;
        }
        finally
        {
            await ExecuteAsync(connection, $"SELECT pg_advisory_unlock({LockKey})", ct);
        }
    }

    private static async Task<Dictionary<string, string>> ReadAppliedAsync(NpgsqlConnection connection, CancellationToken ct)
    {
        var applied = new Dictionary<string, string>(StringComparer.Ordinal);
        await using var command = new NpgsqlCommand("SELECT version, checksum FROM schema_version", connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            applied[reader.GetString(0)] = reader.GetString(1);
        }

        return applied;
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static List<MigrationScript> LoadScripts()
    {
        var assembly = typeof(SqlMigrator).Assembly;
        return assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal) && name.EndsWith(".sql", StringComparison.Ordinal))
            .Select(name =>
            {
                using var stream = assembly.GetManifestResourceStream(name)!;
                using var reader = new StreamReader(stream, Encoding.UTF8);
                var sql = reader.ReadToEnd().ReplaceLineEndings("\n");
                var version = name[ResourcePrefix.Length..^".sql".Length];
                return new MigrationScript(version, sql, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql))));
            })
            .OrderBy(script => script.Version, StringComparer.Ordinal)
            .ToList();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Applied migration {Version}")]
    partial void LogApplied(string version);

    public sealed record MigrationScript(string Version, string Sql, string Checksum);
}
