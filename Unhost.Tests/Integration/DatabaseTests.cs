using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Unhost.Application.Models.Database;
using Unhost.Infrastructure.Database;

namespace Unhost.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class DatabaseTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "unhost-database-tests", Guid.NewGuid().ToString("N"));
    private string ConnectionString => new SqliteConnectionStringBuilder { DataSource = Path.Combine(directory, "state.db"), Pooling = false }.ToString();

    public DatabaseTests() => Directory.CreateDirectory(directory);

    private UnhostDbContext NewContext() => new(new DbContextOptionsBuilder<UnhostDbContext>().UseSqlite(ConnectionString).Options);

    [Fact]
    public async Task Migrations_CanRunTwiceWithoutLosingOperatorData()
    {
        SqliteMigrations.Apply(ConnectionString);
        await using (var context = NewContext())
        {
            context.Operators.Add(new OperatorUser { Id = "retained", Email = "retained@example.com" });
            await context.SaveChangesAsync();
        }

        SqliteMigrations.Apply(ConnectionString);

        await using var reopened = NewContext();
        Assert.Equal("retained@example.com", (await reopened.Operators.SingleAsync()).Email);
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM __EFMigrationsHistory";
        Assert.Equal(2L, command.ExecuteScalar());
    }

    [Fact]
    public void Migrations_RejectUnknownHistoryWithoutChangingIt()
    {
        SqliteMigrations.Apply(ConnectionString);
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE __EFMigrationsHistory SET MigrationId = '20990101000000_Unknown'
            WHERE MigrationId = (SELECT MAX(MigrationId) FROM __EFMigrationsHistory)
            """;
        command.ExecuteNonQuery();

        Assert.Throws<InvalidOperationException>(() => SqliteMigrations.Apply(ConnectionString));

        command.CommandText = "SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId = '20990101000000_Unknown'";
        Assert.Equal(1L, command.ExecuteScalar());
    }

    [Fact]
    public async Task Store_StaleUpdateAndDeleteCannotOverwriteANewerPassword()
    {
        SqliteMigrations.Apply(ConnectionString);
        await using var first = NewContext();
        var store = new EfOperatorStore(first, new IdentityErrorDescriber());
        Assert.True((await store.CreateAsync(new OperatorUser
        {
            Id = "operator", NormalizedUserName = "OPERATOR", NormalizedEmail = "OPERATOR@EXAMPLE.COM",
            PasswordHash = "old-hash", ConcurrencyStamp = "initial"
        }, default)).Succeeded);
        await using var second = NewContext();
        var staleStore = new EfOperatorStore(second, new IdentityErrorDescriber());
        var stale = (await staleStore.FindByEmailAsync("OPERATOR@EXAMPLE.COM", default))!;
        var current = (await store.FindByNameAsync("OPERATOR", default))!;
        current.PasswordHash = "new-hash";
        Assert.True((await store.UpdateAsync(current, default)).Succeeded);

        stale.PasswordHash = "stale-hash";
        Assert.False((await staleStore.UpdateAsync(stale, default)).Succeeded);
        Assert.False((await staleStore.DeleteAsync(stale, default)).Succeeded);

        await using var verification = NewContext();
        Assert.Equal("new-hash", (await verification.Operators.SingleAsync()).PasswordHash);
        Assert.True((await store.DeleteAsync(current, default)).Succeeded);
        Assert.False(await store.Any(default));
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);
}
