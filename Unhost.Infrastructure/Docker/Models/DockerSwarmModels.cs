using System.Text.Json.Serialization;

namespace Unhost.Infrastructure.Docker.Models;

public sealed record DockerSwarmInitRequest(
    string AdvertiseAddr,
    string ListenAddr = "0.0.0.0:2377");

public sealed record DockerSwarmResponse(
    [property: JsonPropertyName("ID")] string Id,
    DockerSwarmJoinTokens JoinTokens);

public sealed record DockerSwarmJoinTokens(string Worker, string Manager)
{
    public override string ToString() => nameof(DockerSwarmJoinTokens);
}

public sealed record DockerNodeResponse(
    [property: JsonPropertyName("ID")] string Id,
    DockerNodeSpec Spec,
    DockerNodeDescription? Description,
    DockerNodeStatus Status,
    DockerManagerStatus? ManagerStatus);

public sealed record DockerManagerStatus(string Addr);

public sealed record DockerNodeSpec(string Role, string Availability);

public sealed record DockerNodeDescription(string Hostname);

public sealed record DockerNodeStatus(string State);
