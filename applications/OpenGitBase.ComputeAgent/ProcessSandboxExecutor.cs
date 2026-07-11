using System.Diagnostics;

namespace OpenGitBase.ComputeAgent;

public sealed class ProcessSandboxExecutor : ISandboxExecutor
{
    public async Task<SandboxExecutionResult> ExecuteAsync(
        string script,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        CancellationToken cancellationToken
    )
    {
        var start = Stopwatch.GetTimestamp();
        var info = new ProcessStartInfo("sh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workingDirectory,
        };
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add(script);
        foreach (var variable in environment)
        {
            info.Environment[variable.Key] = variable.Value;
        }

        using var process = Process.Start(info);
        if (process is null)
        {
            return new SandboxExecutionResult { Success = false, ExitCode = -1, DurationMs = 0 };
        }

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var elapsedMs = (long)Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        return new SandboxExecutionResult
        {
            Success = process.ExitCode == 0,
            ExitCode = process.ExitCode,
            DurationMs = elapsedMs,
        };
    }
}
