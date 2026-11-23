using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Unhost.Infrastructure.Database;

internal sealed class UnhostDbContextFactory : IDesignTimeDbContextFactory<UnhostDbContext>
{
    public UnhostDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<UnhostDbContext>()
            .UseSqlite(UnhostDbContext.DefaultConnectionString)
            .Options;

        return new UnhostDbContext(options);
    }
}
