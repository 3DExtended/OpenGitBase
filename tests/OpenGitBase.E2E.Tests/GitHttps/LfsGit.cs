using System.Text.RegularExpressions;
using OpenGitBase.E2E.Core;

namespace OpenGitBase.E2E.Tests.GitHttps;

/// <summary>git-lfs plumbing shared by the LFS E2E scenarios.</summary>
internal static partial class LfsGit
{
    /// <summary>
    /// The filter config <c>git lfs install</c> would write globally, passed per command so the suite does not
    /// depend on (or change) the machine's git config.
    /// </summary>
    public static readonly string[] FilterConfig =
    [
        "-c", "filter.lfs.process=git-lfs filter-process",
        "-c", "filter.lfs.required=true",
        "-c", "filter.lfs.clean=git-lfs clean -- %f",
        "-c", "filter.lfs.smudge=git-lfs smudge -- %f",
    ];

    /// <summary>curl tracing on stderr, and never a credential prompt.</summary>
    public static readonly IReadOnlyDictionary<string, string> HttpTrace = new Dictionary<string, string>
    {
        ["GIT_CURL_VERBOSE"] = "1",
        ["GIT_TERMINAL_PROMPT"] = "0",
    };

    public static async Task<GitCommandResult> RunAsync(GitOperations gitOps, string workDir, string[] args)
    {
        var result = await gitOps.RunAsync(workDir, args).ConfigureAwait(false);
        Assert.True(result.Succeeded, $"git {string.Join(' ', args)}: {result.StdErr}");
        return result;
    }

    public static async Task RequireGitLfsAsync(GitOperations gitOps)
    {
        var version = await gitOps.RunAsync(E2eEnvironment.RepoRoot, ["lfs", "version"]).ConfigureAwait(false);
        Assert.True(version.Succeeded, "git-lfs must be installed on the machine that runs the E2E suite.");
    }

    /// <summary>A repository in <paramref name="workDir"/> that stores <c>*.bin</c> with LFS.</summary>
    public static async Task InitLfsRepositoryAsync(GitOperations gitOps, string workDir, string username)
    {
        await gitOps.InitRepositoryAsync(workDir, "main", username, $"{username}@example.com").ConfigureAwait(false);
        await RunAsync(gitOps, workDir, ["lfs", "install", "--local"]).ConfigureAwait(false);
        await RunAsync(gitOps, workDir, ["lfs", "track", "*.bin"]).ConfigureAwait(false);
    }

    public static Task<GitCommandResult> CloneAsync(GitOperations gitOps, string remote, string targetDir) =>
        gitOps.RunAsync(
            E2eEnvironment.RepoRoot,
            ["-c", "credential.helper=", .. FilterConfig, "clone", remote, targetDir],
            HttpTrace
        );

    /// <summary>stderr without the curl trace lines, for assertion messages.</summary>
    public static string WithoutTrace(string stderr) =>
        string.Join('\n', stderr.Split('\n').Where(line => !TraceLine().IsMatch(line)));

    public static Task<GitCommandResult> PushAsync(GitOperations gitOps, string workDir, string branch) =>
        gitOps.RunAsync(workDir, ["-c", "credential.helper=", "push", "-u", "origin", branch], HttpTrace);

    [GeneratedRegex(@"^\d{2}:\d{2}:\d{2}\.\d+ ")]
    private static partial Regex TraceLine();
}
