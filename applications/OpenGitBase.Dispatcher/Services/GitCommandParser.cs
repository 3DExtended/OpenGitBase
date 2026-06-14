﻿using System.Text.RegularExpressions;
using OpenGitBase.Dispatcher.Models;

namespace OpenGitBase.Dispatcher.Services;

public sealed class GitCommandParser
{
    private static readonly Regex GitCommandRegex = new(
        @"^(?<command>git-upload-pack|git-receive-pack)\s+['""]?(?<path>[^'""]+)['""]?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant
    );

    public bool TryParse(
        string? sshOriginalCommand,
        out RepositoryOperation operation,
        out string repositoryPath
    )
    {
        operation = RepositoryOperation.Unknown;
        repositoryPath = string.Empty;

        if (string.IsNullOrWhiteSpace(sshOriginalCommand))
        {
            return false;
        }

        var match = GitCommandRegex.Match(sshOriginalCommand.Trim());
        if (!match.Success)
        {
            return false;
        }

        var command = match.Groups["command"].Value;
        repositoryPath = NormalizeRepositoryPath(match.Groups["path"].Value);
        if (string.IsNullOrWhiteSpace(repositoryPath))
        {
            return false;
        }

        operation = command switch
        {
            "git-upload-pack" => RepositoryOperation.ReadGit,
            "git-receive-pack" => RepositoryOperation.WriteGit,
            _ => RepositoryOperation.Unknown,
        };

        return operation != RepositoryOperation.Unknown;
    }

    private static string NormalizeRepositoryPath(string rawPath)
    {
        var path = rawPath.Trim().Trim('\'', '"');
        if (path.StartsWith('/'))
        {
            path = path[1..];
        }

        if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            path = path[..^4];
        }

        return path;
    }
}
