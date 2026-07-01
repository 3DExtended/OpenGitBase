using System.Diagnostics;
using System.Text;

namespace OpenGitBase.E2E.Core;

public sealed class GitRemote
{
    public string Url { get; init; } = string.Empty;
}

public sealed class GitCommandResult
{
    public int ExitCode { get; init; }

    public string StdOut { get; init; } = string.Empty;

    public string StdErr { get; init; } = string.Empty;

    public bool Succeeded => ExitCode == 0;
}

public sealed class GitStateSnapshot
{
    public IReadOnlyList<string> Refs { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> RecentCommits { get; init; } = Array.Empty<string>();

    public string ToSummary()
    {
        var sb = new StringBuilder();
        sb.AppendLine("refs:");
        foreach (var r in Refs)
        {
            sb.AppendLine($"  {r}");
        }

        sb.AppendLine("commits:");
        foreach (var c in RecentCommits)
        {
            sb.AppendLine($"  {c}");
        }

        return sb.ToString();
    }
}

public interface IGitOperations
{
    Task InitRepositoryAsync(string workingDirectory, string branch, string userName, string userEmail, CancellationToken cancellationToken = default);

    Task CommitFileAsync(string workingDirectory, string relativePath, string content, string commitMessage, bool append = false, CancellationToken cancellationToken = default);

    Task CommitPathsAsync(string workingDirectory, string commitMessage, CancellationToken cancellationToken = default);

    Task AddRemoteAsync(string workingDirectory, string remoteName, string remoteUrl, CancellationToken cancellationToken = default);

    Task PushAsync(string workingDirectory, string remoteName, string refSpec, CancellationToken cancellationToken = default);

    Task CloneAsync(string remoteUrl, string targetDir, CancellationToken cancellationToken = default);

    Task<GitCommandResult> TryPushAsync(string workingDirectory, string remoteName, string refSpec, CancellationToken cancellationToken = default);

    Task CheckoutBranchAsync(string workingDirectory, string branch, bool create, CancellationToken cancellationToken = default);

    Task SetRemoteUrlAsync(string workingDirectory, string remoteName, string remoteUrl, CancellationToken cancellationToken = default);
}

public interface IGitAssertions
{
    GitStateSnapshot Inspect(string repoPath);

    void AssertCommitExists(GitStateSnapshot snap, string subject);

    void AssertRef(GitStateSnapshot snap, string refName, string? expectedSha = null);

    void AssertFileContains(string repoPath, string relativePath, string expectedContent);
}

public sealed class GitOperations : IGitOperations
{
    private readonly IOperationTranscript _transcript;

    public GitOperations(IOperationTranscript transcript)
    {
        _transcript = transcript;
    }

    public async Task InitRepositoryAsync(string workingDirectory, string branch, string userName, string userEmail, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(workingDirectory);
        await RunGitAsync(workingDirectory, ["init", "-b", branch], cancellationToken).ConfigureAwait(false);
        await RunGitAsync(workingDirectory, ["config", "user.email", userEmail], cancellationToken).ConfigureAwait(false);
        await RunGitAsync(workingDirectory, ["config", "user.name", userName], cancellationToken).ConfigureAwait(false);
    }

    public async Task CommitFileAsync(string workingDirectory, string relativePath, string content, string commitMessage, bool append = false, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(workingDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        if (append && File.Exists(fullPath))
        {
            content = await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false) + content;
        }

        await File.WriteAllTextAsync(fullPath, content, cancellationToken).ConfigureAwait(false);
        await RunGitAsync(workingDirectory, ["add", relativePath], cancellationToken).ConfigureAwait(false);
        await RunGitAsync(workingDirectory, ["commit", "-m", commitMessage], cancellationToken).ConfigureAwait(false);
    }

    public async Task CommitPathsAsync(string workingDirectory, string commitMessage, CancellationToken cancellationToken = default)
    {
        await RunGitAsync(workingDirectory, ["add", "-A"], cancellationToken).ConfigureAwait(false);
        await RunGitAsync(workingDirectory, ["commit", "-m", commitMessage], cancellationToken).ConfigureAwait(false);
    }

    public Task AddRemoteAsync(string workingDirectory, string remoteName, string remoteUrl, CancellationToken cancellationToken = default) =>
        RunGitAsync(workingDirectory, ["remote", "add", remoteName, remoteUrl], cancellationToken);

    public Task PushAsync(string workingDirectory, string remoteName, string refSpec, CancellationToken cancellationToken = default) =>
        RunGitAsync(workingDirectory, ["-c", "credential.helper=", "push", "-u", remoteName, refSpec], cancellationToken);

    public Task CloneAsync(string remoteUrl, string targetDir, CancellationToken cancellationToken = default) =>
        RunGitAsync(E2eEnvironment.RepoRoot, ["-c", "credential.helper=", "clone", remoteUrl, targetDir], cancellationToken);

    public Task<GitCommandResult> TryPushAsync(string workingDirectory, string remoteName, string refSpec, CancellationToken cancellationToken = default) =>
        ExecuteGitAsync(workingDirectory, ["-c", "credential.helper=", "push", remoteName, refSpec], cancellationToken);

    public Task CheckoutBranchAsync(string workingDirectory, string branch, bool create, CancellationToken cancellationToken = default)
    {
        var args = create ? new[] { "checkout", "-b", branch } : new[] { "checkout", branch };
        return RunGitAsync(workingDirectory, args, cancellationToken);
    }

    public Task SetRemoteUrlAsync(string workingDirectory, string remoteName, string remoteUrl, CancellationToken cancellationToken = default) =>
        RunGitAsync(workingDirectory, ["remote", "set-url", remoteName, remoteUrl], cancellationToken);

    private async Task RunGitAsync(
        string workingDirectory,
        IReadOnlyList<string> args,
        CancellationToken cancellationToken,
        bool throwOnFailure = true)
    {
        var result = await ExecuteGitAsync(workingDirectory, args, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded && throwOnFailure)
        {
            throw new InvalidOperationException($"git {string.Join(' ', args)} failed: {result.StdErr}");
        }
    }

    private async Task<GitCommandResult> ExecuteGitAsync(
        string workingDirectory,
        IReadOnlyList<string> args,
        CancellationToken cancellationToken)
    {
        _transcript.RecordWire(new WireEvent
        {
            Kind = WireEventKind.GitCommand,
            Summary = $"git {string.Join(' ', args)}",
            Detail = workingDirectory,
        });

        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start git.");
        var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        _transcript.RecordWire(new WireEvent
        {
            Kind = WireEventKind.GitOutput,
            Summary = process.ExitCode == 0 ? "git succeeded" : $"git failed ({process.ExitCode})",
            Detail = string.IsNullOrEmpty(stderr) ? stdout : stderr,
        });

        return new GitCommandResult
        {
            ExitCode = process.ExitCode,
            StdOut = stdout,
            StdErr = stderr,
        };
    }
}

public sealed class GitAssertions : IGitAssertions
{
    public GitStateSnapshot Inspect(string repoPath)
    {
        using var repo = new LibGit2Sharp.Repository(repoPath);
        var refs = repo.Refs.Select(r => $"{r.CanonicalName} {r.TargetIdentifier}").ToList();
        var commits = repo.Commits.Take(10).Select(c => $"{c.Sha} {c.MessageShort}").ToList();
        return new GitStateSnapshot { Refs = refs, RecentCommits = commits };
    }

    public void AssertCommitExists(GitStateSnapshot snap, string subject)
    {
        if (!snap.RecentCommits.Any(c => c.Contains(subject, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Expected commit with subject '{subject}' not found.");
        }
    }

    public void AssertRef(GitStateSnapshot snap, string refName, string? expectedSha = null)
    {
        var match = snap.Refs.FirstOrDefault(r => r.Contains(refName, StringComparison.Ordinal));
        if (match == null)
        {
            throw new InvalidOperationException($"Ref '{refName}' not found.");
        }

        if (expectedSha != null && !match.Contains(expectedSha, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Ref '{refName}' does not point to {expectedSha}.");
        }
    }

    public void AssertFileContains(string repoPath, string relativePath, string expectedContent)
    {
        var fullPath = Path.Combine(repoPath, relativePath);
        var content = File.ReadAllText(fullPath);
        if (!content.Contains(expectedContent, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Expected '{relativePath}' to contain '{expectedContent}'.");
        }
    }
}
