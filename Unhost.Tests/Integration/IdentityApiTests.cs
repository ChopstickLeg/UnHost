using System.Net;
using System.Net.Http.Json;
using Unhost.Application.Models.DTO.Response;

namespace Unhost.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class IdentityApiTests
{
    private const string Email = "operator@example.com";
    private const string Password = "Test-Password123!";

    private static Task<HttpResponseMessage> Setup(HttpClient client, string? secret = ApiFactory.BootstrapSecret,
        string password = Password, string email = Email) =>
        client.PostAsJsonAsync("/api/auth/setup", new { email, password, bootstrapSecret = secret });

    private static Task<HttpResponseMessage> Login(HttpClient client, string password = Password, string email = Email) =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password, isPersistent = false });

    [Fact]
    public async Task Setup_CreatesOneOperatorAndCannotReplaceTheAccount()
    {
        using var factory = new ApiFactory();
        using var client = factory.NewClient();
        Assert.False((await client.GetFromJsonAsync<StatusResponse>("/api/status"))!.SetupComplete);

        Assert.Equal(HttpStatusCode.Created, (await Setup(client)).StatusCode);
        Assert.True((await client.GetFromJsonAsync<StatusResponse>("/api/status"))!.SetupComplete);
        Assert.Equal(HttpStatusCode.Forbidden, (await Setup(client, email: "replacement@example.com")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(client, email: "replacement@example.com")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Login(client)).StatusCode);
        Assert.Equal(Email, (await client.GetFromJsonAsync<WhoAmIResponse>("/api/auth/me"))!.Email);
        Assert.True(File.Exists(Path.Combine(factory.DataDirectory, "unhost.db")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("incorrect-secret")]
    public async Task Setup_RejectsInvalidBootstrapWithoutConsumingSetup(string? secret)
    {
        using var factory = new ApiFactory();
        using var client = factory.NewClient();

        Assert.Equal(HttpStatusCode.Forbidden, (await Setup(client, secret)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Setup(client)).StatusCode);
    }

    [Theory]
    [InlineData("weak", "operator@example.com")]
    [InlineData(Password, "not-an-email")]
    [InlineData(Password, " ")]
    public async Task Setup_RejectsInvalidAccountWithoutConsumingSetup(string password, string email)
    {
        using var factory = new ApiFactory();
        using var client = factory.NewClient();

        Assert.Equal(HttpStatusCode.BadRequest, (await Setup(client, password: password, email: email)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Setup(client)).StatusCode);
    }

    [Fact]
    public async Task Setup_ConcurrentRequestsCreateExactlyOneOperator()
    {
        using var factory = new ApiFactory();
        using var first = factory.NewClient();
        using var second = factory.NewClient();
        var responses = await Task.WhenAll(Setup(first), Setup(second, email: "second@example.com"));

        Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Forbidden], responses.Select(r => r.StatusCode).Order());
    }

    [Fact]
    public async Task Authentication_RequiresCookieAndLogoutRemovesIt()
    {
        using var factory = new ApiFactory();
        using var client = factory.NewClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Setup(client)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(client, password: "Wrong-Password123!")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(client, email: "unknown@example.com")).StatusCode);

        var login = await Login(client);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var cookie = Assert.Single(login.Headers.GetValues("Set-Cookie"));
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(Email, (await client.GetFromJsonAsync<WhoAmIResponse>("/api/auth/me"))!.Email);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Login_LocksAccountAfterRepeatedFailures()
    {
        using var factory = new ApiFactory();
        using var client = factory.NewClient();
        Assert.Equal(HttpStatusCode.Created, (await Setup(client)).StatusCode);

        for (var attempt = 0; attempt < 4; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await Login(client, password: "wrong")).StatusCode);
        }

        Assert.Equal((HttpStatusCode)423, (await Login(client, password: "wrong")).StatusCode);
        Assert.Equal((HttpStatusCode)423, (await Login(client)).StatusCode);
    }

    [Fact]
    public async Task CrossOriginSetup_IsRejectedWithoutCreatingAnAccount()
    {
        using var factory = new ApiFactory();
        using var client = factory.NewClient();
        client.DefaultRequestHeaders.Add("Origin", "https://untrusted.example");
        client.DefaultRequestHeaders.Add("Sec-Fetch-Site", "cross-site");

        Assert.Equal(HttpStatusCode.Forbidden, (await Setup(client)).StatusCode);
        client.DefaultRequestHeaders.Clear();
        Assert.False((await client.GetFromJsonAsync<StatusResponse>("/api/status"))!.SetupComplete);
        Assert.Equal(HttpStatusCode.Created, (await Setup(client)).StatusCode);
    }
}
