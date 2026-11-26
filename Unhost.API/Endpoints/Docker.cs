using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Unhost.Application.Models.DTO.Request;
using Unhost.Application.Models.DTO.Response;
using Unhost.Infrastructure.Database;
using Unhost.Infrastructure.Docker;
using Unhost.Infrastructure.Docker.Models;

namespace Unhost.API.Endpoints;

public static class Docker
{
    private static readonly SemaphoreSlim Initialization = new(1);

    public static void MapDockerEndpoints(this RouteGroupBuilder group)
    {
        var docker = group.MapGroup("/docker").RequireAuthorization()
            .AddEndpointFilter(async (context, next) =>
            {
                try
                {
                    return await next(context);
                }
                catch (Exception error) when (IsDockerFailure(error, context.HttpContext.RequestAborted))
                {
                    return Results.Problem("Check the Docker connection and Swarm manager health, then refresh.",
                        statusCode: StatusCodes.Status503ServiceUnavailable, title: "Docker is unavailable");
                }
            });
        docker.MapGet("/cluster", Cluster);
        docker.MapPost("/init", Init);
        docker.MapPost("/import", Import);
        docker.MapGet("/join-commands", JoinCommands);
    }

    private static async Task<IResult> Cluster(
        [FromServices] DockerService docker, [FromServices] LocalSwarmStore store, CancellationToken cancellation)
    {
        DockerInfoResponse? info;
        using (var probeTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
        {
            probeTimeout.CancelAfter(TimeSpan.FromSeconds(3));
            try
            {
                info = await docker.GetInfoAsync(probeTimeout.Token);
            }
            catch (NotSupportedException)
            {
                return TypedResults.Ok(new LocalClusterResponse("incompatible", null, null, false, []));
            }
            catch (Exception error) when (IsDockerFailure(error, cancellation))
            {
                info = null;
            }
        }
        var state = EngineState(info);
        if (state != "manager")
        {
            return TypedResults.Ok(new LocalClusterResponse(state, info?.ServerVersion, null, false, []));
        }

        var swarm = await docker.GetSwarmAsync(cancellation);
        var imported = await store.ContainsAsync(swarm.Id, cancellation);
        var nodes = await docker.ListNodesAsync(cancellation);
        return TypedResults.Ok(new LocalClusterResponse(state, info!.ServerVersion, swarm.Id, imported,
            nodes.Select(node => new ClusterNodeResponse(
                node.Id, string.IsNullOrWhiteSpace(node.Description?.Hostname) ? node.Id : node.Description.Hostname,
                node.Spec.Role, node.Status.State, node.Spec.Availability))
                .ToArray()));
    }

    private static async Task<IResult> Init([FromBody] InitializeLocalSwarmRequest request,
        [FromServices] DockerService docker, [FromServices] LocalSwarmStore store, CancellationToken cancellation)
    {
        var address = request.AdvertiseAddress?.Trim();
        if (string.IsNullOrEmpty(address) || address.Length > 255 || address.Any(char.IsWhiteSpace))
        {
            return Results.Problem("Enter the host IP address or network interface other nodes can reach.", statusCode: 400);
        }

        await Initialization.WaitAsync(cancellation);
        try
        {
            var state = EngineState(await docker.GetInfoAsync(cancellation));
            if (state == "standalone")
            {
                try
                {
                    await docker.InitializeSwarmAsync(new DockerSwarmInitRequest(address), cancellation);
                }
                catch (HttpRequestException error) when (error.StatusCode is HttpStatusCode.BadRequest
                    or HttpStatusCode.InternalServerError or HttpStatusCode.ServiceUnavailable)
                {
                    // Docker may already have applied initialization when the request failed.
                    var currentState = EngineState(await docker.GetInfoAsync(cancellation));
                    if (currentState is "worker" or "unavailable")
                    {
                        return ManagerRequired();
                    }

                    if (currentState == "standalone")
                    {
                        if (error.StatusCode == HttpStatusCode.ServiceUnavailable)
                        {
                            throw;
                        }

                        return Results.Problem("Docker could not initialize Swarm. Verify the advertised address and try again.",
                            statusCode: 422);
                    }
                }
            }
            else if (state != "manager")
            {
                return ManagerRequired();
            }

            return await Register(docker, store, cancellation);
        }
        finally
        {
            Initialization.Release();
        }
    }

    private static async Task<IResult> Import(
        [FromServices] DockerService docker, [FromServices] LocalSwarmStore store, CancellationToken cancellation) =>
        EngineState(await docker.GetInfoAsync(cancellation)) == "manager"
            ? await Register(docker, store, cancellation)
            : ManagerRequired();

    private static async Task<IResult> Register(DockerService docker, LocalSwarmStore store, CancellationToken cancellation)
    {
        var swarm = await docker.GetSwarmAsync(cancellation);
        await store.RegisterAsync(swarm.Id, cancellation);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> JoinCommands(HttpContext context,
        [FromServices] DockerService docker, [FromServices] LocalSwarmStore store, CancellationToken cancellation)
    {
        context.Response.Headers.CacheControl = "no-store";
        var info = await docker.GetInfoAsync(cancellation);
        if (EngineState(info) != "manager")
        {
            return ManagerRequired();
        }

        var swarm = await docker.GetSwarmAsync(cancellation);
        if (!await store.ContainsAsync(swarm.Id, cancellation))
        {
            return Results.Problem("Import this Swarm before requesting join commands.", statusCode: 409);
        }

        var nodes = await docker.ListNodesAsync(cancellation);
        var address = nodes.FirstOrDefault(node => node.Id == info.Swarm?.NodeId)?.ManagerStatus?.Addr;
        if (address is null || !IPEndPoint.TryParse(address, out var endpoint) || endpoint.Port == 0 ||
            swarm.JoinTokens is not { } tokens || !ValidToken(tokens.Worker) || !ValidToken(tokens.Manager))
        {
            return Results.Problem("Docker did not report an advertised manager address and both join tokens.",
                statusCode: 503);
        }

        var target = endpoint.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
            ? $"'{endpoint}'"
            : endpoint.ToString();
        return TypedResults.Ok(new ClusterJoinCommandsResponse(
            $"docker swarm join --token {tokens.Worker} {target}",
            $"docker swarm join --token {tokens.Manager} {target}"));
    }

    private static bool ValidToken(string? token) =>
        token is not null && token.StartsWith("SWMTKN-", StringComparison.Ordinal) &&
        token.All(character => char.IsAsciiLetterOrDigit(character) || character == '-');

    private static IResult ManagerRequired() =>
        Results.Problem("Connect UnHost to a Swarm manager to perform this action.", statusCode: 409);

    private static string EngineState(DockerInfoResponse? info) => info?.Swarm switch
    {
        { LocalNodeState: "inactive" } => "standalone",
        { LocalNodeState: "active", ControlAvailable: true } => "manager",
        { LocalNodeState: "active", ControlAvailable: false } => "worker",
        _ => info is null ? "disconnected" : "unavailable"
    };

    private static bool IsDockerFailure(Exception error, CancellationToken cancellation) =>
        error is HttpRequestException or JsonException or NotSupportedException ||
        error is OperationCanceledException && !cancellation.IsCancellationRequested;
}

[JsonSerializable(typeof(InitializeLocalSwarmRequest))]
[JsonSerializable(typeof(LocalClusterResponse))]
[JsonSerializable(typeof(ClusterJoinCommandsResponse))]
[JsonSerializable(typeof(ProblemDetails))]
[JsonSerializable(typeof(HttpValidationProblemDetails))]
internal partial class AppJsonSerializerContext;
