using System.Diagnostics;

namespace OpenGitBase.E2E.Core;

public interface IComposeEnvironment
{
    Uri ApiBaseUrl { get; }

    Uri WebBaseUrl { get; }

    Uri GitHttpBaseUrl { get; }

    ComposeProfile Profile { get; }

    Task StartAsync(ComposeProfile profile, CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    Task WaitHealthyAsync(CancellationToken cancellationToken = default);
}

public sealed class ComposeEnvironment : IComposeEnvironment, IDisposable
{
    private Process? _composeProcess;

    public Uri ApiBaseUrl => E2eEnvironment.ApiBaseUrl;

    public Uri WebBaseUrl => E2eEnvironment.WebBaseUrl;

    public Uri GitHttpBaseUrl => E2eEnvironment.GitHttpBaseUrl;

    public ComposeProfile Profile { get; private set; } = ComposeProfile.Fast;

    public async Task StartAsync(ComposeProfile profile, CancellationToken cancellationToken = default)
    {
        Profile = profile;
        // docker-compose.e2e.yml enables E2E__CaptureEmail on API services for email capture
        // and internal /internal/e2e/* endpoints used by AuthJourneyTests and reset-database.
        var composeFiles = GetComposeFiles(profile);
        var args = new List<string> { "compose" };
        foreach (var file in composeFiles)
        {
            args.Add("-f");
            args.Add(file);
        }

        args.Add("up");
        args.Add("-d");
        args.Add("--wait");

        await RunDockerAsync(args, cancellationToken).ConfigureAwait(false);
        await WaitHealthyAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        var composeFiles = GetComposeFiles(Profile);
        var args = new List<string> { "compose" };
        foreach (var file in composeFiles)
        {
            args.Add("-f");
            args.Add(file);
        }

        args.Add("down");
        await RunDockerAsync(args, cancellationToken).ConfigureAwait(false);
    }

    public async Task WaitHealthyAsync(CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTime.UtcNow.AddMinutes(5);
        Exception? lastError = null;

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var health = await client.GetAsync(E2eEnvironment.ApiHealthUrl, cancellationToken).ConfigureAwait(false);
                if (health.IsSuccessStatusCode)
                {
                    var apiHealth = await client.GetAsync(new Uri(E2eEnvironment.ApiBaseUrl, "../health"), cancellationToken).ConfigureAwait(false);
                    if (apiHealth.IsSuccessStatusCode)
                    {
                        return;
                    }
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                lastError = ex;
            }

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
        }

        throw new InvalidOperationException(
            $"Compose stack did not become healthy at {E2eEnvironment.ApiHealthUrl}.",
            lastError);
    }

    public void Dispose()
    {
        _composeProcess?.Dispose();
    }

    private static IReadOnlyList<string> GetComposeFiles(ComposeProfile profile)
    {
        var root = E2eEnvironment.RepoRoot;
        var files = new List<string> { Path.Combine(root, "docker-compose.yml") };
        var e2eOverride = Path.Combine(root, "docker-compose.e2e.yml");
        if (File.Exists(e2eOverride))
        {
            files.Add(e2eOverride);
        }

        if (profile == ComposeProfile.FullHa)
        {
            var fullHa = Path.Combine(root, "docker-compose.e2e-full-ha.yml");
            if (File.Exists(fullHa))
            {
                files.Add(fullHa);
            }
        }

        return files;
    }

    private async Task RunDockerAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "docker",
            WorkingDirectory = E2eEnvironment.RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start docker compose.");
        _composeProcess = process;
        var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"docker {string.Join(' ', args)} failed with exit code {process.ExitCode}.\n{stdout}\n{stderr}");
        }
    }
}
