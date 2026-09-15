using App.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace App.IntegrationTests;

/// <summary>
/// The <c>ddl-auto: validate</c> of this starter (ADR-007): after the SQL scripts ran, every table, column
/// (type and nullability), primary key and index that the EF model expects must exist in the database.
/// Extra database objects (schema_version, future columns) are allowed; a missing or differently typed one fails.
/// </summary>
[Collection(AppCollection.Name)]
public sealed class SchemaValidationTests(AppFactory factory)
{
    [Fact]
    public async Task Database_schema_matches_the_ef_model()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var actual = await ReadDatabaseAsync(factory.ConnectionString);

        var problems = new List<string>();
        foreach (var table in context.Model.GetRelationalModel().Tables)
        {
            if (!actual.Columns.TryGetValue(table.Name, out var columns))
            {
                problems.Add($"table {table.Name} is missing");
                continue;
            }

            foreach (var column in table.Columns.Where(c => c.Name != "xmin"))
            {
                if (!columns.TryGetValue(column.Name, out var actualColumn))
                {
                    problems.Add($"{table.Name}.{column.Name} is missing");
                    continue;
                }

                if (!string.Equals(actualColumn.StoreType, column.StoreType, StringComparison.Ordinal))
                {
                    problems.Add($"{table.Name}.{column.Name} is {actualColumn.StoreType}, model expects {column.StoreType}");
                }

                if (actualColumn.IsNullable != column.IsNullable)
                {
                    problems.Add($"{table.Name}.{column.Name} nullability is {actualColumn.IsNullable}, model expects {column.IsNullable}");
                }
            }

            var expectedIndexes = table.Indexes.Select(i => i.Name).Concat(table.PrimaryKey is { } pk ? [pk.Name] : []);
            foreach (var index in expectedIndexes)
            {
                if (!actual.Indexes.Contains(index!))
                {
                    problems.Add($"index or constraint {index} on {table.Name} is missing");
                }
            }
        }

        Assert.True(problems.Count == 0, "Schema drift between SQL migrations and the EF model:\n  " + string.Join("\n  ", problems));
    }

    private static async Task<DatabaseSchema> ReadDatabaseAsync(string connectionString)
    {
        var columns = new Dictionary<string, Dictionary<string, DatabaseColumn>>(StringComparer.Ordinal);
        var indexes = new HashSet<string>(StringComparer.Ordinal);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using (var command = new NpgsqlCommand("""
            SELECT c.table_name, c.column_name, c.is_nullable = 'YES', format_type(a.atttypid, a.atttypmod)
            FROM information_schema.columns c
            JOIN pg_class r ON r.relname = c.table_name
            JOIN pg_namespace n ON n.oid = r.relnamespace AND n.nspname = c.table_schema
            JOIN pg_attribute a ON a.attrelid = r.oid AND a.attname = c.column_name
            WHERE c.table_schema = 'public'
            """, connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var table = columns.TryGetValue(reader.GetString(0), out var existing) ? existing : columns[reader.GetString(0)] = new(StringComparer.Ordinal);
                table[reader.GetString(1)] = new DatabaseColumn(reader.GetString(3), reader.GetBoolean(2));
            }
        }

        await using (var command = new NpgsqlCommand("SELECT indexname FROM pg_indexes WHERE schemaname = 'public'", connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                indexes.Add(reader.GetString(0));
            }
        }

        return new DatabaseSchema(columns, indexes);
    }

    private sealed record DatabaseColumn(string StoreType, bool IsNullable);

    private sealed record DatabaseSchema(Dictionary<string, Dictionary<string, DatabaseColumn>> Columns, HashSet<string> Indexes);
}
