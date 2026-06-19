using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using OpenGitBase.Dispatcher.Models;

namespace OpenGitBase.Dispatcher.Services;

public sealed class GitSmartHttpPathParser
{
    private static readonly Regex GitSmartHttpPathRegex = new(
        @"^/(?<owner>[^/]+)/(?<repo>[^/]+)\.git/(?<suffix>info/refs|git-upload-pack|git-receive-pack)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    );

    public bool TryParse(
        PathString path,
        QueryString query,
        out GitSmartHttpRequest request
    )
    {
        request = new GitSmartHttpRequest();

        var match = GitSmartHttpPathRegex.Match(path.Value ?? string.Empty);
        if (!match.Success)
        {
            return false;
        }

        var owner = match.Groups["owner"].Value;
        var repo = match.Groups["repo"].Value;
        var suffix = match.Groups["suffix"].Value.ToLowerInvariant();

        request = new GitSmartHttpRequest
        {
            RepositoryPath = $"{owner}/{repo}",
            GitSuffix = suffix,
        };

        return suffix switch
        {
            "info/refs" => TryParseInfoRefs(query, request),
            "git-upload-pack" => AssignOperation(request, RepositoryOperation.ReadGit),
            "git-receive-pack" => AssignOperation(request, RepositoryOperation.WriteGit),
            _ => false,
        };
    }

    private static bool TryParseInfoRefs(QueryString query, GitSmartHttpRequest request)
    {
        string? service = null;
        if (query.HasValue)
        {
            var parsed = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(query.Value);
            if (parsed.TryGetValue("service", out var values))
            {
                service = values.ToString();
            }
        }

        request.Operation = service switch
        {
            "git-upload-pack" => RepositoryOperation.ReadGit,
            "git-receive-pack" => RepositoryOperation.WriteGit,
            _ => RepositoryOperation.Unknown,
        };

        return request.Operation != RepositoryOperation.Unknown;
    }

    private static bool AssignOperation(
        GitSmartHttpRequest request,
        RepositoryOperation operation
    )
    {
        request.Operation = operation;
        return true;
    }
}
