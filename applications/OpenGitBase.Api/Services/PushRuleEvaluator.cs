﻿using System.Text.Json;
using System.Text.RegularExpressions;
using OpenGitBase.Api.Models;
using OpenGitBase.Features.Repository.Contracts;

namespace OpenGitBase.Api.Services;

public static partial class PushRuleEvaluator
{
    public static GitPushEnforcementResult EvaluateCommits(
        IReadOnlyList<PushRuleDto> pushRules,
        IReadOnlyList<GitPushCommitRequest> commits
    )
    {
        if (pushRules.Count == 0 || commits.Count == 0)
        {
            return GitPushEnforcementResult.Allow();
        }

        foreach (var commit in commits)
        {
            foreach (var rule in pushRules)
            {
                var result = EvaluateCommit(rule, commit);
                if (!result.Allowed)
                {
                    return result;
                }
            }
        }

        return GitPushEnforcementResult.Allow();
    }

    private static GitPushEnforcementResult EvaluateCommit(
        PushRuleDto rule,
        GitPushCommitRequest commit
    ) =>
        rule.RuleType switch
        {
            PushRuleType.MaxFileSize => EvaluateMaxFileSize(rule, commit),
            PushRuleType.ForbiddenPaths => EvaluateForbiddenPaths(rule, commit),
            PushRuleType.CommitMessageRegex => EvaluateCommitMessageRegex(rule, commit),
            PushRuleType.RequireDco => EvaluateRequireDco(rule, commit),
            _ => GitPushEnforcementResult.Allow(),
        };

    private static GitPushEnforcementResult EvaluateMaxFileSize(
        PushRuleDto rule,
        GitPushCommitRequest commit
    )
    {
        if (!TryReadLong(rule.ConfigJson, "maxBytes", out var maxBytes) || maxBytes <= 0)
        {
            return GitPushEnforcementResult.Allow();
        }

        if (commit.MaxBlobBytes <= maxBytes)
        {
            return GitPushEnforcementResult.Allow();
        }

        return GitPushEnforcementResult.Deny(
            $"Push rule MaxFileSize rejected commit {ShortSha(commit.Sha)}: "
                + $"file exceeds limit of {maxBytes} bytes."
        );
    }

    private static GitPushEnforcementResult EvaluateForbiddenPaths(
        PushRuleDto rule,
        GitPushCommitRequest commit
    )
    {
        if (!TryReadStringArray(rule.ConfigJson, "globs", out var globs) || globs.Count == 0)
        {
            return GitPushEnforcementResult.Allow();
        }

        foreach (var path in commit.ChangedPaths)
        {
            foreach (var glob in globs)
            {
                if (PathGlobMatcher.IsMatch(path, glob))
                {
                    return GitPushEnforcementResult.Deny(
                        $"Push rule ForbiddenPaths rejected commit {ShortSha(commit.Sha)}: "
                            + $"path '{path}' matches forbidden glob '{glob}'."
                    );
                }
            }
        }

        return GitPushEnforcementResult.Allow();
    }

    private static GitPushEnforcementResult EvaluateCommitMessageRegex(
        PushRuleDto rule,
        GitPushCommitRequest commit
    )
    {
        if (!TryReadString(rule.ConfigJson, "regex", out var pattern)
            || string.IsNullOrWhiteSpace(pattern))
        {
            return GitPushEnforcementResult.Allow();
        }

        if (Regex.IsMatch(commit.Message, pattern, RegexOptions.Multiline))
        {
            return GitPushEnforcementResult.Allow();
        }

        return GitPushEnforcementResult.Deny(
            $"Push rule CommitMessageRegex rejected commit {ShortSha(commit.Sha)}: "
                + "commit message does not match required pattern."
        );
    }

    private static GitPushEnforcementResult EvaluateRequireDco(
        PushRuleDto rule,
        GitPushCommitRequest commit
    )
    {
        if (TryReadBool(rule.ConfigJson, "required", out var required) && !required)
        {
            return GitPushEnforcementResult.Allow();
        }

        if (SignedOffByLine().IsMatch(commit.Message))
        {
            return GitPushEnforcementResult.Allow();
        }

        return GitPushEnforcementResult.Deny(
            $"Push rule RequireDco rejected commit {ShortSha(commit.Sha)}: "
                + "missing Signed-off-by line."
        );
    }

    private static string ShortSha(string sha) =>
        sha.Length >= 7 ? sha[..7] : sha;

    private static bool TryReadLong(string configJson, string propertyName, out long value)
    {
        value = 0;
        if (!TryReadElement(configJson, out var root))
        {
            return false;
        }

        if (root.TryGetProperty(propertyName, out var property)
            && property.TryGetInt64(out value))
        {
            return true;
        }

        return false;
    }

    private static bool TryReadString(string configJson, string propertyName, out string value)
    {
        value = string.Empty;
        if (!TryReadElement(configJson, out var root))
        {
            return false;
        }

        if (root.TryGetProperty(propertyName, out var property))
        {
            value = property.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }

        return false;
    }

    private static bool TryReadBool(string configJson, string propertyName, out bool value)
    {
        value = false;
        if (!TryReadElement(configJson, out var root))
        {
            return false;
        }

        if (root.TryGetProperty(propertyName, out var property)
            && (property.ValueKind == JsonValueKind.True
                || property.ValueKind == JsonValueKind.False))
        {
            value = property.GetBoolean();
            return true;
        }

        return false;
    }

    private static bool TryReadStringArray(
        string configJson,
        string propertyName,
        out IReadOnlyList<string> values
    )
    {
        values = [];
        if (!TryReadElement(configJson, out var root))
        {
            return false;
        }

        if (!root.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var items = new List<string>();
        foreach (var item in property.EnumerateArray())
        {
            var text = item.GetString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                items.Add(text);
            }
        }

        values = items;
        return items.Count > 0;
    }

    private static bool TryReadElement(string configJson, out JsonElement root)
    {
        root = default;
        if (string.IsNullOrWhiteSpace(configJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(configJson);
            root = document.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    [GeneratedRegex(
        @"^Signed-off-by:\s+.+$",
        RegexOptions.Multiline | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex SignedOffByLine();
}
