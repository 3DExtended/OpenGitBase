using System.Diagnostics;

namespace OpenGitBase.Features.Pipeline.Tests.QueryHandlers;

/// <summary>A throwaway bare repository with a single commit, for handlers that shell out to git.</summary>
internal sealed class GitTestRepository : IDisposable
{
    private readonly string _workDir;

    private GitTestRepository(string workDir, string path, string commitSha)
    {
        _workDir = workDir;
        Path = path;
        CommitSha = commitSha;
    }

    public string Path { get; }

    public string CommitSha { get; }

    public static async Task<GitTestRepository> CreateAsync(IReadOnlyDictionary<string, string> files)
    {
        var workDir = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"ogb-git-test-{Guid.NewGuid():N}"
        );
        var sourcePath = System.IO.Path.Combine(workDir, "source");
        var repositoryPath = System.IO.Path.Combine(workDir, "repo.git");
        Directory.CreateDirectory(workDir);

        await RunGitAsync(workDir, "init", "-b", "main", sourcePath);
        foreach (var (name, content) in files)
        {
            await File.WriteAllTextAsync(System.IO.Path.Combine(sourcePath, name), content);
            await RunGitAsync(sourcePath, "add", name);
        }

        await RunGitAsync(
            sourcePath,
            "-c",
            "user.name=OGB Test",
            "-c",
            "user.email=test@example.com",
            "-c",
            "commit.gpgsign=false",
            "commit",
            "-m",
            "init"
        );
        await RunGitAsync(workDir, "clone", "--bare", sourcePath, repositoryPath);
        var commitSha = (await RunGitAsync(repositoryPath, "rev-parse", "HEAD")).Trim();
        return new GitTestRepository(workDir, repositoryPath, commitSha);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workDir))
        {
            Directory.Delete(_workDir, recursive: true);
        }
    }

    private static async Task<string> RunGitAsync(string workingDirectory, params string[] arguments)
    {
        var info = new ProcessStartInfo("git")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            WorkingDirectory = workingDirectory,
        };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(
            process.ExitCode == 0,
            $"git {string.Join(' ', arguments)} failed: {await stderr}"
        );
        return await stdout;
    }
}
