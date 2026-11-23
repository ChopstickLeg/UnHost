using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Unhost.Application.Models.Database;
using Unhost.Application.Models.DTO.Request;
using Unhost.Application.Models.DTO.Response;
using Unhost.Infrastructure.Database;

namespace Unhost.API.Endpoints;

public static class Identity
{
    public static void MapIdentityEndpoints(this RouteGroupBuilder group)
    {
        var authGroup = group.MapGroup("/auth");

        authGroup.MapPost("/setup", SetupIdentity);
        authGroup.MapPost("/login", Login);
        authGroup.MapGet("/me", WhoAmI)
            .RequireAuthorization();
        authGroup.MapPost("/logout", Logout)
            .RequireAuthorization();
    }

    private static async Task<IResult> SetupIdentity(SetupRequest request, [FromServices] FirstRunSetup setup, IConfiguration configuration, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return TypedResults.BadRequest();
        }

        var secretPath = configuration["UnHost:BootstrapSecretFile"];
        if (string.IsNullOrEmpty(secretPath) || string.IsNullOrEmpty(request.BootstrapSecret))
        {
            return TypedResults.Forbid();
        }

        var expectedSecret = (await File.ReadAllTextAsync(secretPath, cancellationToken)).TrimEnd('\r', '\n');
        if (expectedSecret.Length == 0 || !CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedSecret), Encoding.UTF8.GetBytes(request.BootstrapSecret)))
        {
            return TypedResults.Forbid();
        }

        var result = await setup.TryCreateAdmin(new OperatorUser
        {
            UserName = request.Email,
            Email = request.Email
        }, request.Password, cancellationToken);

        if (result is null)
        {
            return TypedResults.Forbid();
        }
        if (!result.Succeeded)
        {
            return TypedResults.BadRequest(result.Errors);
        }
        return TypedResults.Created();
    }

    private static async Task<IResult> Login(LoginRequest request, SignInManager<OperatorUser> signInManager, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return TypedResults.BadRequest();
        }

        var user = await signInManager.UserManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            return TypedResults.Unauthorized();
        }
        var result = await signInManager.PasswordSignInAsync(user, request.Password, isPersistent: request.IsPersistent, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            return TypedResults.StatusCode(423);
        }
        else if (!result.Succeeded)
        {
            return TypedResults.Unauthorized();
        }
        return TypedResults.Ok();
    }

    private static async Task<IResult> WhoAmI(UserManager<OperatorUser> userManager, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(httpContext.User);
        if (user == null)
        {
            return TypedResults.Unauthorized();
        }
        return TypedResults.Ok(new WhoAmIResponse(user.Email!));
    }

    private static async Task<IResult> Logout(SignInManager<OperatorUser> signInManager)
    {
        await signInManager.SignOutAsync();
        return TypedResults.Ok();
    }
}

[JsonSerializable(typeof(WhoAmIResponse))]
[JsonSerializable(typeof(SetupRequest))]
[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(IEnumerable<IdentityError>))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{

}
