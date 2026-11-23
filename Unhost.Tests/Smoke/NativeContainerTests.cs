using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Unhost.Application.Models.DTO.Response;
using Xunit.Abstractions;

namespace Unhost.Tests.Smoke;

[Trait("Category", "Smoke")]
public sealed class NativeContainerTests(ITestOutputHelper output)
{
    [Fact]
    public async Task NativeAotContainer_BootstrapAuthenticationAndSessionSurviveRecreation()
    {
        await using var container = new NativeContainer(output);
        await container.StartAsync();
        var client = container.Client;
        Assert.False((await client.GetFromJsonAsync<StatusResponse>("/api/status"))!.SetupComplete);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/auth/logout", null)).StatusCode);

        var html = await client.GetStringAsync("/");
        Assert.Contains("<div id=\"app\">", html);
        var assets = Regex.Matches(html, "(?:src|href)=\"(/assets/[^\"]+)\"").Select(match => match.Groups[1].Value).ToArray();
        Assert.Contains(assets, asset => asset.EndsWith(".js", StringComparison.Ordinal));
        Assert.Contains(assets, asset => asset.EndsWith(".css", StringComparison.Ordinal));
        foreach (var asset in assets)
        {
            using var response = await client.GetAsync(asset);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotEmpty(await response.Content.ReadAsByteArrayAsync());
        }

        const string email = "smoke@example.com";
        const string password = "NativeAot-Smoke123!";
        var setup = new { email, password, bootstrapSecret = container.BootstrapSecret };
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/auth/setup", new { email, password, bootstrapSecret = "incorrect" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/setup", new { email, password = "weak", bootstrapSecret = container.BootstrapSecret })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/auth/setup", setup)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/auth/setup", setup)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { email, password = "wrong" })).StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password, isPersistent = false });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var setCookie = Assert.Single(login.Headers.GetValues("Set-Cookie"));
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        client.DefaultRequestHeaders.Add("Cookie", setCookie.Split(';')[0]);
        Assert.Equal(email, (await client.GetFromJsonAsync<WhoAmIResponse>("/api/auth/me"))!.Email);

        await container.RecreateAsync();

        Assert.True((await client.GetFromJsonAsync<StatusResponse>("/api/status"))!.SetupComplete);
        Assert.Equal(email, (await client.GetFromJsonAsync<WhoAmIResponse>("/api/auth/me"))!.Email);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/auth/setup", new
        {
            email = "replacement@example.com", password, bootstrapSecret = container.BootstrapSecret
        })).StatusCode);
        var logout = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        Assert.Contains(logout.Headers.GetValues("Set-Cookie"), value => value.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase));
        client.DefaultRequestHeaders.Remove("Cookie");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { email = "replacement@example.com", password })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new { email, password })).StatusCode);
    }
}
