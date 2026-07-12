using System.Diagnostics;

namespace OpenGitBase.Cli.Auth;

internal static class KeychainProcessRunner
{
    public static string EscapeForShell(string value) => value.Replace("\"", "\\\"", StringComparison.Ordinal);

    public static string Run(string arguments, bool captureOutput = false)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "/usr/bin/security",
            Arguments = arguments,
            RedirectStandardOutput = captureOutput,
            RedirectStandardError = captureOutput,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        process.Start();
        var output = captureOutput ? process.StandardOutput.ReadToEnd() : string.Empty;
        var error = captureOutput ? process.StandardError.ReadToEnd() : string.Empty;
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"security command failed ({process.ExitCode}): {error}".Trim());
        }

        return output;
    }
}
