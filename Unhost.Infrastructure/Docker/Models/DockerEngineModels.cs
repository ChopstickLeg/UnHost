using System.Text.Json.Serialization;

namespace Unhost.Infrastructure.Docker.Models;

public sealed record DockerVersionResponse(
    string Version,
    string ApiVersion,
    [property: JsonPropertyName("MinAPIVersion")] string MinApiVersion);

public sealed record DockerInfoResponse(string ServerVersion, DockerSwarmInfo? Swarm);

public sealed record DockerSwarmInfo(string LocalNodeState, bool ControlAvailable,
    [property: JsonPropertyName("NodeID")] string? NodeId);
