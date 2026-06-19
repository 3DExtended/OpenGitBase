using Microsoft.AspNetCore.Http;
using OpenGitBase.Dispatcher.Models;
using OpenGitBase.Dispatcher.Services;

namespace OpenGitBase.Dispatcher.Tests.Services;

public class GitSmartHttpPathParserTests
{
    private readonly GitSmartHttpPathParser _parser = new();

    [Theory]
    [InlineData(
        "/alice/demo.git/info/refs",
        "?service=git-upload-pack",
        RepositoryOperation.ReadGit,
        "alice/demo",
        "info/refs"
    )]
    [InlineData(
        "/alice/demo.git/info/refs",
        "?service=git-receive-pack",
        RepositoryOperation.WriteGit,
        "alice/demo",
        "info/refs"
    )]
    [InlineData(
        "/org/repo.git/git-upload-pack",
        "",
        RepositoryOperation.ReadGit,
        "org/repo",
        "git-upload-pack"
    )]
    [InlineData(
        "/org/repo.git/git-receive-pack",
        "",
        RepositoryOperation.WriteGit,
        "org/repo",
        "git-receive-pack"
    )]
    public void TryParse_ParsesSmartHttpPaths(
        string path,
        string query,
        RepositoryOperation expectedOperation,
        string expectedRepositoryPath,
        string expectedSuffix
    )
    {
        var parsed = _parser.TryParse(
            path,
            string.IsNullOrEmpty(query) ? QueryString.Empty : new QueryString(query),
            out var request);

        Assert.True(parsed);
        Assert.Equal(expectedOperation, request.Operation);
        Assert.Equal(expectedRepositoryPath, request.RepositoryPath);
        Assert.Equal(expectedSuffix, request.GitSuffix);
    }

    [Theory]
    [InlineData("/alice/demo.git/info/refs", "")]
    [InlineData("/alice/demo.git/info/refs", "?service=unknown")]
    [InlineData("/alice/demo.git/unknown", "")]
    [InlineData("/api/v1/status", "")]
    public void TryParse_RejectsUnsupportedPaths(string path, string query)
    {
        var parsed = _parser.TryParse(
            path,
            string.IsNullOrEmpty(query) ? QueryString.Empty : new QueryString(query),
            out _);

        Assert.False(parsed);
    }
}
