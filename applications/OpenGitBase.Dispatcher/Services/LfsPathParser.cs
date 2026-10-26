using System.Text.RegularExpressions;
using OpenGitBase.Dispatcher.Models;

namespace OpenGitBase.Dispatcher.Services;

/// <summary>Recognises Git LFS requests under <c>/{owner}/{repo}.git/info/lfs/</c>; git paths never match.</summary>
public sealed partial class LfsPathParser
{
    public bool TryParse(string method, string? path, out LfsHttpRequest request)
    {
        request = new LfsHttpRequest();
        var match = LfsPathRegex().Match(path ?? string.Empty);
        if (!match.Success)
        {
            return false;
        }

        var repositoryPath = $"{match.Groups["owner"].Value}/{match.Groups["repo"].Value}";
        var rest = match.Groups["rest"].Value;
        var (kind, oid) = Classify(method, rest);
        request = new LfsHttpRequest { RepositoryPath = repositoryPath, Kind = kind, Oid = oid };
        return true;
    }

    private static (LfsRequestKind Kind, string? Oid) Classify(string method, string rest)
    {
        if (rest == "objects/batch")
        {
            return (HttpMethodIs(method, "POST") ? LfsRequestKind.Batch : LfsRequestKind.Unsupported, null);
        }

        if (rest == "locks/verify")
        {
            return (HttpMethodIs(method, "POST") ? LfsRequestKind.LocksVerify : LfsRequestKind.Unsupported, null);
        }

        var objectMatch = ObjectPathRegex().Match(rest);
        if (!objectMatch.Success)
        {
            return (LfsRequestKind.Unsupported, null);
        }

        var oid = objectMatch.Groups["oid"].Value;
        var kind = objectMatch.Groups["verify"].Success
            ? HttpMethodIs(method, "POST") ? LfsRequestKind.Verify : LfsRequestKind.Unsupported
            : HttpMethodIs(method, "PUT") ? LfsRequestKind.Upload
            : HttpMethodIs(method, "GET") ? LfsRequestKind.Download
            : LfsRequestKind.Unsupported;
        return (kind, kind == LfsRequestKind.Unsupported ? null : oid);
    }

    private static bool HttpMethodIs(string method, string expected) =>
        string.Equals(method, expected, StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(
        @"^/(?<owner>[^/]+)/(?<repo>[^/]+)\.git/info/lfs/(?<rest>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    private static partial Regex LfsPathRegex();

    [GeneratedRegex(@"^objects/(?<oid>[0-9a-f]{64})(?<verify>/verify)?$", RegexOptions.CultureInvariant)]
    private static partial Regex ObjectPathRegex();
}
