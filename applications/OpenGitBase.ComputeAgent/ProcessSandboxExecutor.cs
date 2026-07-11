using System.Diagnostics;

namespace OpenGitBase.ComputeAgent;

public sealed class ProcessSandboxExecutor : ISandboxExecutor
{
    public async Task<bool> ExecuteAsync(string script, CancellationToken cancellationToken)
    {
        var info = new ProcessStartInfo("sh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add(script);
        using var process = Process.Start(info);
        if (process is null)
        {
            return false;
        }

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return process.ExitCode == 0;
    }
}
