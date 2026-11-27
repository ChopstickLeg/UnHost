using System.Text.Json.Serialization;

namespace Unhost.Infrastructure.Docker.Models;

[JsonSerializable(typeof(DockerVersionResponse))]
[JsonSerializable(typeof(DockerInfoResponse))]
[JsonSerializable(typeof(DockerSwarmInitRequest))]
[JsonSerializable(typeof(DockerSwarmResponse))]
[JsonSerializable(typeof(DockerNodeResponse[]))]
[JsonSerializable(typeof(string))]
internal sealed partial class DockerJsonContext : JsonSerializerContext;
