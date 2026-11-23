using Microsoft.Data.Sqlite;

namespace Unhost.Infrastructure.Database;

public static class SqliteMigrations
{
    private const string ResourcePrefix = "Unhost.Infrastructure.Database.Migrations.Sql.";
    private const string ResourceSuffix = ".sql";

    public static void Apply(string connectionString)
    {
        var assembly = typeof(SqliteMigrations).Assembly;
        var scripts = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal) &&
                name.EndsWith(ResourceSuffix, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (scripts.Length == 0)
        {
            throw new InvalidOperationException("No SQLite migration scripts are embedded.");
        }

        var options = new SqliteConnectionStringBuilder(connectionString) { ForeignKeys = false, Pooling = false };
        using var connection = new SqliteConnection(options.ConnectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction(deferred: false);
        using var command = connection.CreateCommand();

        var expected = scripts.Select(name => name[ResourcePrefix.Length..^ResourceSuffix.Length]).ToArray();
        var applied = ReadHistory(command);
        if (!applied.SequenceEqual(expected.Take(applied.Count), StringComparer.Ordinal))
        {
            throw new InvalidOperationException("The SQLite migration history does not match this UnHost build.");
        }

        if (applied.Count == scripts.Length)
        {
            return;
        }

        foreach (var script in scripts.Skip(applied.Count))
        {
            using var stream = assembly.GetManifestResourceStream(script)!;
            using var reader = new StreamReader(stream);
            command.CommandText = reader.ReadToEnd();
            command.ExecuteNonQuery();
        }

        if (!ReadHistory(command).SequenceEqual(expected, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("The SQLite scripts did not record the expected migrations.");
        }

        command.CommandText = "PRAGMA foreign_key_check";
        using (var reader = command.ExecuteReader())
        {
            if (reader.Read())
            {
                throw new InvalidOperationException("A SQLite migration left foreign key violations.");
            }
        }

        transaction.Commit();
    }

    private static List<string> ReadHistory(SqliteCommand command)
    {
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM sqlite_schema WHERE type = 'table' AND name = '__EFMigrationsHistory')";
        if ((long)command.ExecuteScalar()! == 0)
        {
            return [];
        }

        command.CommandText = "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId";
        using var reader = command.ExecuteReader();
        var applied = new List<string>();
        while (reader.Read())
        {
            applied.Add(reader.GetString(0));
        }

        return applied;
    }
}
