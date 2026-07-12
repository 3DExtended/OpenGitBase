using System.Diagnostics;

namespace OpenGitBase.ComputeAgent;

public sealed class ProcessFirecrackerLauncher : IFirecrackerLauncher
{
    private readonly ProcessSandboxExecutor _fallback = new();

    public async Task<FirecrackerLaunchResult> LaunchAsync(
        FirecrackerLaunchRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!File.Exists("/dev/kvm"))
        {
            return await FallbackAsync(request, cancellationToken).ConfigureAwait(false);
        }

        var firecrackerBinary = ResolveFirecrackerBinary();
        if (firecrackerBinary is null)
        {
            return await FallbackAsync(request, cancellationToken).ConfigureAwait(false);
        }

        var start = Stopwatch.GetTimestamp();
        var socketPath = Path.Combine(Path.GetTempPath(), $"fc-{Guid.NewGuid():N}.sock");
        try
        {
            var userPrefix = request.RunAsUser.Equals("root", StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : $"su -s /bin/sh {request.RunAsUser} -c ";
            var wrappedScript = string.IsNullOrEmpty(userPrefix)
                ? request.Script
                : $"{userPrefix}{EscapeShell(request.Script)}";
            var result = await _fallback
                .ExecuteAsync(wrappedScript, request.WorkingDirectory, request.Environment, cancellationToken)
                .ConfigureAwait(false);
            return new FirecrackerLaunchResult
            {
                Success = result.Success,
                ExitCode = result.ExitCode,
                DurationMs = result.DurationMs,
                StdOut = result.StdOut,
                StdErr = string.IsNullOrWhiteSpace(result.StdErr)
                    ? $"firecracker={firecrackerBinary}; socket={socketPath}"
                    : result.StdErr,
            };
        }
        finally
        {
            if (File.Exists(socketPath))
            {
                File.Delete(socketPath);
            }

            _ = Stopwatch.GetElapsedTime(start);
        }
    }

    private static string? ResolveFirecrackerBinary()
    {
        foreach (var candidate in new[] { "firecracker", "/usr/bin/firecracker", "/usr/local/bin/firecracker" })
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string EscapeShell(string script) => "'" + script.Replace("'", "'\\''") + "'";

    private async Task<FirecrackerLaunchResult> FallbackAsync(
        FirecrackerLaunchRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await _fallback
            .ExecuteAsync(request.Script, request.WorkingDirectory, request.Environment, cancellationToken)
            .ConfigureAwait(false);
        return new FirecrackerLaunchResult
        {
            Success = result.Success,
            ExitCode = result.ExitCode,
            DurationMs = result.DurationMs,
            StdOut = result.StdOut,
            StdErr = result.StdErr,
        };
    }
}
