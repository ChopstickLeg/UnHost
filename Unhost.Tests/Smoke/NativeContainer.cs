using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using Xunit.Abstractions;

namespace Unhost.Tests.Smoke;

internal sealed class NativeContainer(ITestOutputHelper output) : IAsyncDisposable
{
    private readonly string name = "unhost-smoke-" + Guid.NewGuid().ToString("N");
    private readonly string directory = Path.Combine(Path.GetTempPath(), "unhost-smoke", Guid.NewGuid().ToString("N"));
    private string Volume => name + "-data";
    private string Image => Environment.GetEnvironmentVariable("UNHOST_SMOKE_IMAGE")
        ?? throw new InvalidOperationException("Set UNHOST_SMOKE_IMAGE to the locally built Native AOT image. Use Category!=Smoke for tests without Docker.");
    public string BootstrapSecret { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    public HttpClient Client { get; } = new(new HttpClientHandler { UseCookies = false, UseProxy = false, AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    public async Task StartAsync()
    {
        _ = Image;
        Directory.CreateDirectory(directory);
        var secretPath = Path.Combine(directory, "bootstrap.txt");
        await File.WriteAllTextAsync(secretPath, BootstrapSecret);
        await RunContainerAsync("127.0.0.1::8080");
        var portMapping = await Docker("port", name, "8080/tcp");
        var port = int.Parse(portMapping.Trim().Split(':')[^1], System.Globalization.CultureInfo.InvariantCulture);
        Client.BaseAddress = new Uri($"http://127.0.0.1:{port}");
        await WaitUntilReadyAsync();
    }

    public async Task RecreateAsync()
    {
        await Docker("rm", "--force", name);
        // Reuse the published port because BaseAddress is fixed after the first request.
        var port = Client.BaseAddress!.Port;
        await RunContainerAsync($"127.0.0.1:{port}:8080");
        await WaitUntilReadyAsync();
    }

    private Task<string> RunContainerAsync(string portBinding) =>
        Docker("run", "--detach", "--name", name, "--publish", portBinding,
            "--env", "UnHost__BootstrapSecretFile=/run/secrets/bootstrap",
            "--mount", $"type=volume,source={Volume},target=/data",
            "--mount", $"type=bind,source={Path.Combine(directory, "bootstrap.txt")},target=/run/secrets/bootstrap,readonly", Image);

    private async Task WaitUntilReadyAsync()
    {
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < TimeSpan.FromSeconds(45))
        {
            try
            {
                using var response = await Client.GetAsync("/api/status");
                if (response.StatusCode == HttpStatusCode.OK) return;
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) { }
            await Task.Delay(250);
        }
        throw new TimeoutException("Native AOT container did not become ready.\n" + await Docker("logs", name));
    }

    private static async Task<string> Docker(params string[] arguments)
    {
        var info = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true, RedirectStandardError = true,
            UseShellExecute = false, CreateNoWindow = true
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Docker failed to start.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }
        var result = await stdout;
        var errors = await stderr;
        if (process.ExitCode != 0) throw new InvalidOperationException($"docker {arguments[0]} failed: {errors}\n{result}");
        return result + errors;
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        // Cleanup is best effort so diagnostics never hide the original assertion failure.
        foreach (var arguments in new[] { new[] { "logs", name }, new[] { "rm", "--force", name }, new[] { "volume", "rm", Volume } })
        {
            try { output.WriteLine(await Docker(arguments)); }
            catch (Exception error) when (error is InvalidOperationException or OperationCanceledException
                or System.ComponentModel.Win32Exception or IOException)
            {
                output.WriteLine(error.Message);
            }
        }
        try
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            output.WriteLine(error.Message);
        }
    }
}
