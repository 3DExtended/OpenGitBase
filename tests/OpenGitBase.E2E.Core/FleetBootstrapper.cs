using System.Diagnostics;

namespace OpenGitBase.E2E.Core;

public static class FleetBootstrapper
{
    public static async Task BootstrapAsync(CancellationToken cancellationToken = default)
    {
        var script = Path.Combine(E2eEnvironment.RepoRoot, "scripts", "bootstrap-fleet.sh");
        if (!File.Exists(script))
        {
            throw new InvalidOperationException($"Missing fleet bootstrap script: {script}");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = script,
            WorkingDirectory = E2eEnvironment.RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.Environment["API_URL"] = $"http://localhost:{E2eEnvironment.GitHttpPort}";

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start bootstrap-fleet.sh.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(stdout))
        {
            Console.WriteLine(stdout.TrimEnd());
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"bootstrap-fleet.sh failed with exit code {process.ExitCode}.\n{stderr}".TrimEnd());
        }
    }
}
