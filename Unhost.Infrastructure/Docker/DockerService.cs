using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Unhost.Infrastructure.Docker.Models;

namespace Unhost.Infrastructure.Docker;

public sealed class DockerService : IDisposable
{
    private static readonly Version MinimumApiVersion = new(1, 40);
    private static readonly Version MaximumApiVersion = new(1, 44);
    private static readonly DockerJsonContext Json = DockerJsonContext.Default;

    private readonly HttpClient _http;
    private volatile string? _apiPrefix;

    public DockerService(string socketPath = "/var/run/docker.sock")
    {
        var endpoint = new UnixDomainSocketEndPoint(socketPath);
        _http = new HttpClient(new SocketsHttpHandler
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            ConnectCallback = async (_, cancellationToken) =>
            {
                var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                try
                {
                    await socket.ConnectAsync(endpoint, cancellationToken);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            }
        })
        {
            BaseAddress = new Uri("http://localhost"),
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    public async Task<DockerVersionResponse> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync("/version", cancellationToken);
        return await ReadAsync(response, Json.DockerVersionResponse, cancellationToken);
    }

    public Task<DockerInfoResponse> GetInfoAsync(CancellationToken cancellationToken = default) =>
        GetAsync("/info", Json.DockerInfoResponse, cancellationToken);

    public async Task<DockerSwarmResponse> GetSwarmAsync(CancellationToken cancellationToken = default)
    {
        var swarm = await GetAsync("/swarm", Json.DockerSwarmResponse, cancellationToken);
        return !string.IsNullOrWhiteSpace(swarm.Id)
            ? swarm
            : throw new JsonException("Docker returned a Swarm without an ID.");
    }

    public async Task<DockerNodeResponse[]> ListNodesAsync(CancellationToken cancellationToken = default)
    {
        var nodes = await GetAsync("/nodes", Json.DockerNodeResponseArray, cancellationToken);
        if (nodes.Any(node => node is null || string.IsNullOrWhiteSpace(node.Id) ||
            string.IsNullOrWhiteSpace(node.Spec?.Role) || string.IsNullOrWhiteSpace(node.Spec.Availability) ||
            string.IsNullOrWhiteSpace(node.Status?.State)))
        {
            throw new JsonException("Docker returned incomplete Swarm node information.");
        }

        return nodes;
    }

    public async Task<string> InitializeSwarmAsync(
        DockerSwarmInitRequest request, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/swarm/init")
        {
            Content = JsonContent.Create(request, Json.DockerSwarmInitRequest)
        };
        return await SendAsync(message, Json.String, cancellationToken);
    }

    public void Dispose() => _http.Dispose();

    private async Task<T> GetAsync<T>(
        string path, JsonTypeInfo<T> type, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        return await SendAsync(request, type, cancellationToken);
    }

    private async Task<T> SendAsync<T>(
        HttpRequestMessage request, JsonTypeInfo<T> type, CancellationToken cancellationToken)
    {
        var prefix = await GetApiPrefixAsync(cancellationToken);
        request.RequestUri = new Uri(prefix + request.RequestUri, UriKind.Relative);
        try
        {
            using var response = await _http.SendAsync(request, cancellationToken);
            return await ReadAsync(response, type, cancellationToken);
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException)
        {
            _apiPrefix = null;
            throw;
        }
    }

    private async Task<string> GetApiPrefixAsync(CancellationToken cancellationToken)
    {
        if (_apiPrefix is { } prefix)
        {
            return prefix;
        }

        var daemon = await GetVersionAsync(cancellationToken);
        if (!Version.TryParse(daemon.ApiVersion, out var daemonMax) ||
            !Version.TryParse(daemon.MinApiVersion, out var daemonMin))
        {
            throw new JsonException("Docker returned an invalid Engine API version range.");
        }

        var selected = daemonMax < MaximumApiVersion ? daemonMax : MaximumApiVersion;
        if (selected < MinimumApiVersion || selected < daemonMin)
        {
            throw new NotSupportedException(
                $"Docker Engine API {daemonMin} to {daemonMax} is incompatible with {MinimumApiVersion} to {MaximumApiVersion}.");
        }

        return _apiPrefix = $"/v{selected}";
    }

    private static async Task<T> ReadAsync<T>(
        HttpResponseMessage response, JsonTypeInfo<T> type, CancellationToken cancellationToken)
    {
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(type, cancellationToken)
            ?? throw new JsonException("Docker returned a null response.");
    }
}
