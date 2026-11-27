using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;
using Unhost.Application.Models.DTO.Response;
using Unhost.Infrastructure.Database;

namespace Unhost.API.Endpoints;

public static class Status
{
    public static void MapStatusEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/status", GetStatus);
    }

    private static async Task<IResult> GetStatus([FromServices] EfOperatorStore efOperatorStore,
        CancellationToken cancellation)
    {
        var setupComplete = await efOperatorStore.Any(cancellation);
        return TypedResults.Ok(new StatusResponse(DateTimeOffset.UtcNow, setupComplete));
    }
}

[JsonSerializable(typeof(StatusResponse))]
internal partial class AppJsonSerializerContext;
