﻿namespace OpenGitBase.Api.Services;

public static class GitShaHelper
{
    public const string NullSha = "0000000000000000000000000000000000000000";

    public static bool IsNullSha(string? sha) =>
        string.IsNullOrWhiteSpace(sha)
        || string.Equals(sha, NullSha, StringComparison.OrdinalIgnoreCase);

    public static bool IsDeletion(string? newSha) => IsNullSha(newSha);

    public static bool IsRefCreation(string? oldSha) => IsNullSha(oldSha);

    public static string? TryGetBranchName(string refName)
    {
        const string headsPrefix = "refs/heads/";
        if (!refName.StartsWith(headsPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var branchName = refName[headsPrefix.Length..];
        return string.IsNullOrWhiteSpace(branchName) ? null : branchName;
    }
}
