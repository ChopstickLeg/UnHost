namespace Unhost.Application.Models.DTO.Response;

public sealed record LocalClusterResponse(string State, string? EngineVersion, string? SwarmId, bool Imported, ClusterNodeResponse[] Nodes);

public sealed record ClusterNodeResponse(string Id, string Hostname, string Role, string Readiness, string Availability);

public sealed record ClusterJoinCommandsResponse(string Worker, string Manager)
{
    public override string ToString() => nameof(ClusterJoinCommandsResponse);
}

