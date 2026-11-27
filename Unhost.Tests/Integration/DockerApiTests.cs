using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Unhost.Application.Models.DTO.Response;

namespace Unhost.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class DockerApiTests
{
    private const string ClusterUrl = "/api/docker/cluster";
    private const string InitUrl = "/api/docker/init";
    private const string ImportUrl = "/api/docker/import";
    private const string JoinUrl = "/api/docker/join-commands";

    [Fact]
    public async Task Init_AppliedBeforeDockerError_RegistersSameSwarmOnConcurrentRetries()
    {
        await using var engine = await Engine.StartAsync("inactive");
        using var factory = new ApiFactory { DockerSocketPath = engine.SocketPath };
        using var client = factory.NewClient();
        await SignInAsync(client);
        Assert.Equal("standalone", (await ReadClusterAsync(client)).State);

        using var rejected = await client.PostAsJsonAsync(InitUrl, new { advertiseAddress = "bad-address" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, rejected.StatusCode);
        Assert.Equal("standalone", (await ReadClusterAsync(client)).State);

        var responses = await Task.WhenAll(
            client.PostAsJsonAsync(InitUrl, new { advertiseAddress = " eth0 " }),
            client.PostAsJsonAsync(InitUrl, new { advertiseAddress = " eth0 " }));
        foreach (var response in responses)
        {
            using (response) Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        using var import = await client.PostAsync(ImportUrl, null);
        Assert.Equal(HttpStatusCode.NoContent, import.StatusCode);
        var cluster = await ReadClusterAsync(client);
        Assert.Equal("swarm-test", cluster.SwarmId);
        Assert.True(cluster.Imported);
        Assert.Equal(2, engine.InitCount);
        Assert.Equal("eth0", engine.AdvertiseAddress);
        Assert.True(engine.VersionRequests > 1);
    }

    [Theory]
    [InlineData("active", "1.40", "worker")]
    [InlineData("locked", "1.40", "unavailable")]
    [InlineData("inactive", "1.45", "incompatible")]
    public async Task Cluster_IdentifiesEngineWithNoManagerAccess(string nodeState, string minimumApi, string expected)
    {
        await using var engine = await Engine.StartAsync(nodeState, minimumApi);
        using var factory = new ApiFactory { DockerSocketPath = engine.SocketPath };
        using var client = factory.NewClient();
        await SignInAsync(client);

        var cluster = await ReadClusterAsync(client);
        Assert.Equal(expected, cluster.State);
        Assert.Null(cluster.SwarmId);
        Assert.Empty(cluster.Nodes);
        if (expected == "worker")
        {
            using var init = await client.PostAsJsonAsync(InitUrl, new { advertiseAddress = "eth0" });
            using var import = await client.PostAsync(ImportUrl, null);
            using var join = await client.GetAsync(JoinUrl);
            Assert.Equal(HttpStatusCode.Conflict, init.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, import.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, join.StatusCode);
            Assert.Equal(0, engine.InitCount);
        }
    }

    [Fact]
    public async Task JoinCommands_RequiresAuthenticationAndImport_AndReturnsUncachedRoleCommands()
    {
        await using var engine = await Engine.StartAsync("active", manager: true);
        using var factory = new ApiFactory { DockerSocketPath = engine.SocketPath };
        using var client = factory.NewClient();
        using var cluster = await client.GetAsync(ClusterUrl);
        using var init = await client.PostAsJsonAsync(InitUrl, new { advertiseAddress = "eth0" });
        using var import = await client.PostAsync(ImportUrl, null);
        using var join = await client.GetAsync(JoinUrl);
        Assert.All(new[] { cluster, init, import, join }, response =>
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode));

        await SignInAsync(client);
        using var beforeImport = await client.GetAsync(JoinUrl);
        Assert.Equal(HttpStatusCode.Conflict, beforeImport.StatusCode);

        client.DefaultRequestHeaders.Add("Origin", "https://untrusted.example");
        client.DefaultRequestHeaders.Add("Sec-Fetch-Site", "cross-site");
        using var crossOriginImport = await client.PostAsync(ImportUrl, null);
        using var crossOriginInit = await client.PostAsJsonAsync(InitUrl, new { advertiseAddress = "eth0" });
        Assert.Equal(HttpStatusCode.Forbidden, crossOriginImport.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, crossOriginInit.StatusCode);
        client.DefaultRequestHeaders.Remove("Origin");
        client.DefaultRequestHeaders.Remove("Sec-Fetch-Site");
        var unregistered = await ReadClusterAsync(client);
        Assert.False(unregistered.Imported);
        Assert.Collection(unregistered.Nodes,
            node => Assert.Equal(new ClusterNodeResponse("worker-node", "Alpha", "worker", "ready", "active"), node),
            node => Assert.Equal(new ClusterNodeResponse("manager-node", "Zulu", "manager", "ready", "active"), node));

        using var registered = await client.PostAsync(ImportUrl, null);
        Assert.Equal(HttpStatusCode.NoContent, registered.StatusCode);
        using var response = await client.GetAsync(JoinUrl);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var commands = Assert.IsType<ClusterJoinCommandsResponse>(
            await response.Content.ReadFromJsonAsync<ClusterJoinCommandsResponse>());
        Assert.Equal("docker swarm join --token SWMTKN-1-test-worker '[2001:db8::1]:2377'", commands.Worker);
        Assert.Equal("docker swarm join --token SWMTKN-1-test-manager '[2001:db8::1]:2377'", commands.Manager);
        Assert.DoesNotContain("SWMTKN-", commands.ToString());

        engine.OmitReadiness = true;
        using var malformed = await client.GetAsync(ClusterUrl);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, malformed.StatusCode);
    }

    private static async Task<LocalClusterResponse> ReadClusterAsync(HttpClient client) =>
        Assert.IsType<LocalClusterResponse>(await client.GetFromJsonAsync<LocalClusterResponse>(ClusterUrl));

    private static async Task SignInAsync(HttpClient client)
    {
        const string email = "operator@example.com";
        const string password = "Test-Password123!";
        using var setup = await client.PostAsJsonAsync("/api/auth/setup",
            new { email, password, bootstrapSecret = ApiFactory.BootstrapSecret });
        Assert.Equal(HttpStatusCode.Created, setup.StatusCode);
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password, isPersistent = false });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    private sealed class Engine(WebApplication app, string socketPath, string nodeState, bool manager) : IAsyncDisposable
    {
        private volatile string _nodeState = nodeState;
        private volatile bool _manager = manager;
        private int _versionRequests;
        private int _initCount;

        public string SocketPath { get; } = socketPath;
        public int InitCount => Volatile.Read(ref _initCount);
        public int VersionRequests => Volatile.Read(ref _versionRequests);
        public string? AdvertiseAddress { get; private set; }
        public bool OmitReadiness { get; set; }

        public static async Task<Engine> StartAsync(string nodeState, string minimumApi = "1.40", bool manager = false)
        {
            var socketPath = Path.Combine(Path.GetTempPath(), $"unhost-{Guid.NewGuid():N}.sock");
            var builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.ConfigureKestrel(options => options.ListenUnixSocket(socketPath));
            builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.PropertyNamingPolicy = null);
            var app = builder.Build();
            var engine = new Engine(app, socketPath, nodeState, manager);
            app.MapGet("/version", () =>
            {
                Interlocked.Increment(ref engine._versionRequests);
                return new { Version = "28.5.0", ApiVersion = "1.51", MinAPIVersion = minimumApi };
            });
            app.MapGet("/v1.44/info", () => new
            {
                ServerVersion = "28.5.0",
                Swarm = new { LocalNodeState = engine._nodeState, ControlAvailable = engine._manager, NodeID = "manager-node" },
            });
            app.MapPost("/v1.44/swarm/init", async Task<IResult> (HttpRequest request) =>
            {
                Interlocked.Increment(ref engine._initCount);
                using var body = await JsonDocument.ParseAsync(request.Body, cancellationToken: request.HttpContext.RequestAborted);
                engine.AdvertiseAddress = body.RootElement.GetProperty("AdvertiseAddr").GetString();
                if (engine.AdvertiseAddress != "eth0") return Results.BadRequest();
                engine._nodeState = "active";
                engine._manager = true;
                return Results.StatusCode(500);
            });
            app.MapGet("/v1.44/swarm", () => new
            {
                ID = "swarm-test",
                JoinTokens = new { Worker = "SWMTKN-1-test-worker", Manager = "SWMTKN-1-test-manager" },
            });
            app.MapGet("/v1.44/nodes", () => new[]
            {
                Node("manager-node", "Zulu", "manager", engine.OmitReadiness ? null : "ready", "[2001:db8::1]:2377"),
                Node("worker-node", "Alpha", "worker", "ready", null),
            });
            try
            {
                await app.StartAsync();
                return engine;
            }
            catch
            {
                await engine.DisposeAsync();
                throw;
            }
        }

        private static object Node(string id, string hostname, string role, string? readiness, string? address) => new
        {
            ID = id,
            Description = new { Hostname = hostname },
            Spec = new { Role = role, Availability = "active" },
            Status = new { State = readiness },
            ManagerStatus = address is null ? null : new { Addr = address },
        };

        public async ValueTask DisposeAsync()
        {
            await app.DisposeAsync();
            File.Delete(SocketPath);
        }
    }
}
