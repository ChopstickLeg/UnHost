using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace Unhost.Tests.Integration;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private bool cleanedUp;
    public string DataDirectory { get; } = Path.Combine(Path.GetTempPath(), "unhost-tests", Guid.NewGuid().ToString("N"));
    public const string BootstrapSecret = "test-only-bootstrap-secret-with-enough-entropy";

    public ApiFactory()
    {
        Directory.CreateDirectory(DataDirectory);
        File.WriteAllText(Path.Combine(DataDirectory, "bootstrap.txt"), BootstrapSecret + "\r\n");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("UnHost:DataDirectory", DataDirectory);
        builder.UseSetting("UnHost:BootstrapSecretFile", Path.Combine(DataDirectory, "bootstrap.txt"));
    }

    public HttpClient NewClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false
    });

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && !cleanedUp)
        {
            cleanedUp = true;
            SqliteConnection.ClearAllPools();
            Directory.Delete(DataDirectory, recursive: true);
        }
    }
}
