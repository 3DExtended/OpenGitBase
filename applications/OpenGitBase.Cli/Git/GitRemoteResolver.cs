using System.Text.RegularExpressions;

namespace OpenGitBase.Cli.Git;

public sealed partial class GitRemoteResolver : IGitRemoteResolver
{
    public bool TryResolveFromWorkingDirectory(string? workingDirectory, out RepoSlug slug)
    {
        slug = null!;
        var directory = workingDirectory ?? Directory.GetCurrentDirectory();
        var gitDir = FindGitDirectory(directory);
        if (gitDir is null)
        {
            return false;
        }

        var configPath = Path.Combine(gitDir, "config");
        if (!File.Exists(configPath))
        {
            return false;
        }

        var originUrl = ReadOriginUrl(configPath);
        return originUrl is not null && TryParseRemoteUrl(originUrl, out slug);
    }

    public bool TryParseRepoOption(string? repoOption, out RepoSlug slug)
    {
        slug = null!;
        if (string.IsNullOrWhiteSpace(repoOption))
        {
            return false;
        }

        var parts = repoOption.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            return false;
        }

        slug = new RepoSlug { Owner = parts[0], Slug = parts[1] };
        return true;
    }

    public bool TryParseRemoteUrl(string remoteUrl, out RepoSlug slug)
    {
        slug = null!;
        if (string.IsNullOrWhiteSpace(remoteUrl))
        {
            return false;
        }

        var trimmed = remoteUrl.Trim();
        foreach (var regex in new[] { HttpsRemoteRegex(), ScpRemoteRegex(), SshRemoteRegex() })
        {
            var match = regex.Match(trimmed);
            if (!match.Success)
            {
                continue;
            }

            slug = new RepoSlug
            {
                Owner = match.Groups["owner"].Value,
                Slug = match.Groups["slug"].Value,
            };
            return true;
        }

        return false;
    }

    [GeneratedRegex(@"^https?://[^/]+/(?<owner>[^/]+)/(?<slug>[^/]+?)(?:\.git)?/?$", RegexOptions.IgnoreCase)]
    private static partial Regex HttpsRemoteRegex();

    [GeneratedRegex(@"^[^@]+@[^:]+:(?<owner>[^/]+)/(?<slug>[^/]+?)(?:\.git)?/?$", RegexOptions.IgnoreCase)]
    private static partial Regex ScpRemoteRegex();

    [GeneratedRegex(@"^ssh://[^/]+/(?<owner>[^/]+)/(?<slug>[^/]+?)(?:\.git)?/?$", RegexOptions.IgnoreCase)]
    private static partial Regex SshRemoteRegex();

    private static string? FindGitDirectory(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);
        while (current is not null)
        {
            var gitPath = Path.Combine(current.FullName, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                return gitPath;
            }

            current = current.Parent;
        }

        return null;
    }

    private static string? ReadOriginUrl(string configPath)
    {
        var lines = File.ReadAllLines(configPath);
        var inOrigin = false;
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.StartsWith('['))
            {
                inOrigin = string.Equals(line, "[remote \"origin\"]", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (inOrigin && line.StartsWith("url =", StringComparison.OrdinalIgnoreCase))
            {
                return line.Substring("url =".Length).Trim();
            }
        }

        return null;
    }
}
