using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Unhost.Application.Models.Database;

namespace Unhost.Infrastructure.Database;

public sealed class EfOperatorStore(UnhostDbContext context, IdentityErrorDescriber errors) :
    IUserPasswordStore<OperatorUser>,
    IUserEmailStore<OperatorUser>,
    IUserSecurityStampStore<OperatorUser>,
    IUserLockoutStore<OperatorUser>
{
    public async Task<IdentityResult> CreateAsync(OperatorUser user, CancellationToken cancellationToken)
    {
        context.Operators.Add(user);
        await context.SaveChangesAsync(cancellationToken);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> UpdateAsync(OperatorUser user, CancellationToken cancellationToken)
    {
        var originalStamp = user.ConcurrencyStamp;
        context.Attach(user);
        user.ConcurrencyStamp = Guid.NewGuid().ToString();
        context.Operators.Update(user);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            context.Entry(user).State = EntityState.Detached;
            user.ConcurrencyStamp = originalStamp;
            return IdentityResult.Failed(errors.ConcurrencyFailure());
        }
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> DeleteAsync(OperatorUser user, CancellationToken cancellationToken)
    {
        context.Operators.Remove(user);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            context.Entry(user).State = EntityState.Detached;
            return IdentityResult.Failed(errors.ConcurrencyFailure());
        }
        return IdentityResult.Success;
    }

    public Task<OperatorUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        // EF 11's precompiler requires captured query inputs to be locals.
        var database = context;
        var id = userId;
        var cancellation = cancellationToken;
        return database.Operators.SingleOrDefaultAsync(user => user.Id == id, cancellation);
    }

    public Task<OperatorUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
    {
        var database = context;
        var userName = normalizedUserName;
        var cancellation = cancellationToken;
        return database.Operators.SingleOrDefaultAsync(
            user => user.NormalizedUserName == userName, cancellation);
    }

    public Task<OperatorUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        var database = context;
        var email = normalizedEmail;
        var cancellation = cancellationToken;
        return database.Operators.SingleOrDefaultAsync(
            user => user.NormalizedEmail == email, cancellation);
    }

    public Task<bool> Any(CancellationToken cancellationToken)
    {
        var database = context;
        var cancellation = cancellationToken;
        return database.Operators.AnyAsync(cancellation);
    }

    public Task<string> GetUserIdAsync(OperatorUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.Id);

    public Task<string?> GetUserNameAsync(OperatorUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.UserName);

    public Task SetUserNameAsync(OperatorUser user, string? userName, CancellationToken cancellationToken)
    {
        user.UserName = userName;
        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedUserNameAsync(OperatorUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.NormalizedUserName);

    public Task SetNormalizedUserNameAsync(OperatorUser user, string? normalizedName, CancellationToken cancellationToken)
    {
        user.NormalizedUserName = normalizedName;
        return Task.CompletedTask;
    }

    public Task SetPasswordHashAsync(OperatorUser user, string? passwordHash, CancellationToken cancellationToken)
    {
        user.PasswordHash = passwordHash;
        return Task.CompletedTask;
    }

    public Task<string?> GetPasswordHashAsync(OperatorUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.PasswordHash);

    public Task<bool> HasPasswordAsync(OperatorUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.PasswordHash is not null);

    public Task SetEmailAsync(OperatorUser user, string? email, CancellationToken cancellationToken)
    {
        user.Email = email;
        return Task.CompletedTask;
    }

    public Task<string?> GetEmailAsync(OperatorUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.Email);

    public Task<bool> GetEmailConfirmedAsync(OperatorUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.EmailConfirmed);

    public Task SetEmailConfirmedAsync(OperatorUser user, bool confirmed, CancellationToken cancellationToken)
    {
        user.EmailConfirmed = confirmed;
        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedEmailAsync(OperatorUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.NormalizedEmail);

    public Task SetNormalizedEmailAsync(OperatorUser user, string? normalizedEmail, CancellationToken cancellationToken)
    {
        user.NormalizedEmail = normalizedEmail;
        return Task.CompletedTask;
    }

    public Task SetSecurityStampAsync(OperatorUser user, string stamp, CancellationToken cancellationToken)
    {
        user.SecurityStamp = stamp;
        return Task.CompletedTask;
    }

    public Task<string?> GetSecurityStampAsync(OperatorUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.SecurityStamp);

    public Task<DateTimeOffset?> GetLockoutEndDateAsync(OperatorUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.LockoutEnd);

    public Task SetLockoutEndDateAsync(OperatorUser user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken)
    {
        user.LockoutEnd = lockoutEnd;
        return Task.CompletedTask;
    }

    public Task<int> IncrementAccessFailedCountAsync(OperatorUser user, CancellationToken cancellationToken) =>
        Task.FromResult(++user.AccessFailedCount);

    public Task ResetAccessFailedCountAsync(OperatorUser user, CancellationToken cancellationToken)
    {
        user.AccessFailedCount = 0;
        return Task.CompletedTask;
    }

    public Task<int> GetAccessFailedCountAsync(OperatorUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.AccessFailedCount);

    public Task<bool> GetLockoutEnabledAsync(OperatorUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.LockoutEnabled);

    public Task SetLockoutEnabledAsync(OperatorUser user, bool enabled, CancellationToken cancellationToken)
    {
        user.LockoutEnabled = enabled;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        // The request scope owns the context shared by setup and the store.
    }
}
