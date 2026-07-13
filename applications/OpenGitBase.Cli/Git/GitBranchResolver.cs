using System.Diagnostics;

namespace OpenGitBase.Cli.Git;

public sealed class GitBranchResolver : IGitBranchResolver
{
    public bool TryGetCurrentBranch(string? workingDirectory, out string branchName)
    {
        branchName = null!;
        var directory = workingDirectory ?? Directory.GetCurrentDirectory();
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "rev-parse --abbrev-ref HEAD",
                WorkingDirectory = directory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output) || output == "HEAD")
            {
                return false;
            }

            branchName = output;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
