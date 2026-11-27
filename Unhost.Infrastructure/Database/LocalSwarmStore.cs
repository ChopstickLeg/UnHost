using Microsoft.EntityFrameworkCore;

namespace Unhost.Infrastructure.Database;

public sealed class LocalSwarmStore(UnhostDbContext context)
{
    public Task<bool> ContainsAsync(string id, CancellationToken cancellationToken)
    {
        var database = context;
        var swarmId = id;
        var cancellation = cancellationToken;
        return database.Set<LocalSwarm>().AnyAsync(swarm => swarm.Id == swarmId, cancellation);
    }

    public Task RegisterAsync(string id, CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlAsync(
            $"INSERT INTO LocalSwarms (Id) VALUES ({id}) ON CONFLICT (Id) DO NOTHING", cancellationToken);
}

internal sealed class LocalSwarm
{
    public required string Id { get; set; }
}
