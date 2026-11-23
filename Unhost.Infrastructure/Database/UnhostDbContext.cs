using Microsoft.EntityFrameworkCore;
using Unhost.Application.Models.Database;

namespace Unhost.Infrastructure.Database;

public sealed class UnhostDbContext(DbContextOptions<UnhostDbContext> options) : DbContext(options)
{
    public const string DefaultConnectionString = "Data Source=unhost.db;Foreign Keys=True";

    public DbSet<OperatorUser> Operators => Set<OperatorUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OperatorUser>(user =>
        {
            user.ToTable("AspNetUsers");
            // Public properties avoid invalid generated field accessor names for IdentityUser<string>.
            user.UsePropertyAccessMode(PropertyAccessMode.Property);
            user.HasIndex(x => x.NormalizedUserName).IsUnique().HasDatabaseName("UserNameIndex");
            user.HasIndex(x => x.NormalizedEmail).IsUnique().HasDatabaseName("EmailIndex");
            user.Property(x => x.ConcurrencyStamp).IsConcurrencyToken();
        });

        modelBuilder.Entity<InstallationSetup>(setup =>
        {
            setup.ToTable("InstallationSetup", table => table.HasCheckConstraint("CK_InstallationSetup_SingleRow", "Id = 1"));
            setup.Property(x => x.Id).ValueGeneratedNever();
        });
    }
}

internal sealed class InstallationSetup
{
    public int Id { get; set; }
}
