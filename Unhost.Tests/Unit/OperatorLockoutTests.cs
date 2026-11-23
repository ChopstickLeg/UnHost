using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Unhost.Application.Models.Database;
using Unhost.Infrastructure.Database;

namespace Unhost.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class OperatorLockoutTests
{
    [Fact]
    public async Task AccessFailures_AccumulateAndResetWithoutChangingAnotherOperator()
    {
        using var context = new UnhostDbContext(new DbContextOptionsBuilder<UnhostDbContext>().Options);
        var store = new EfOperatorStore(context, new IdentityErrorDescriber());
        var user = new OperatorUser();
        var otherUser = new OperatorUser { AccessFailedCount = 3 };

        Assert.Equal(1, await store.IncrementAccessFailedCountAsync(user, default));
        Assert.Equal(2, await store.IncrementAccessFailedCountAsync(user, default));
        Assert.Equal(2, await store.GetAccessFailedCountAsync(user, default));

        await store.ResetAccessFailedCountAsync(user, default);

        Assert.Equal(0, await store.GetAccessFailedCountAsync(user, default));
        Assert.Equal(3, await store.GetAccessFailedCountAsync(otherUser, default));
    }
}
