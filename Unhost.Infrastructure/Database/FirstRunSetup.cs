using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Unhost.Application.Models.Database;

namespace Unhost.Infrastructure.Database;

public sealed class FirstRunSetup(UnhostDbContext context, UserManager<OperatorUser> users)
{
    public async Task<IdentityResult?> TryCreateAdmin(
        OperatorUser user, string password, CancellationToken cancellationToken)
    {
        var database = context;
        var cancellation = cancellationToken;
        await database.Database.OpenConnectionAsync(cancellationToken);
        var connection = (SqliteConnection)database.Database.GetDbConnection();
        using var sqliteTransaction = connection.BeginTransaction(deferred: false);
        await using var transaction = await database.Database.UseTransactionAsync(sqliteTransaction, cancellationToken);
        if (await database.Set<InstallationSetup>().AnyAsync(cancellation) ||
            await database.Operators.AnyAsync(cancellation))
        {
            return null;
        }

        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            return result;
        }

        database.Add(new InstallationSetup { Id = 1 });
        await database.SaveChangesAsync(cancellationToken);
        await transaction!.CommitAsync(cancellationToken);
        return result;
    }
}
