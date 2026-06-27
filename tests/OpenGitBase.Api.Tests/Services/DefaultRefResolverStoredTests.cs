#pragma warning disable SA1402 // File may only contain a single type
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;

namespace OpenGitBase.Api.Tests.Services;

public class DefaultRefResolverStoredTests
{
    [Fact]
    public void Resolve_WithStoredDefault_ReturnsStoredWhenBranchExists()
    {
        var branches = new List<RepositoryContentRefDto>
        {
            new() { Name = "main", CommitSha = "abc" },
            new() { Name = "develop", CommitSha = "def" },
        };

        var result = DefaultRefResolver.Resolve(branches, "develop");

        Assert.Equal("develop", result);
    }

    [Fact]
    public void Resolve_WithStoredDefaultMissingBranch_ReturnsStoredName()
    {
        var branches = new List<RepositoryContentRefDto>
        {
            new() { Name = "main", CommitSha = "abc" },
        };

        var result = DefaultRefResolver.Resolve(branches, "release");

        Assert.Equal("release", result);
    }
}

public class RepositoryBranchPatternMatcherTests
{
    [Fact]
    public void ResolvePattern_DefaultAlias_ReturnsDefaultBranch()
    {
        var result = RepositoryBranchPatternMatcher.ResolvePattern("@default", "main");

        Assert.Equal("main", result);
    }

    [Fact]
    public void Matches_WildcardPrefix_MatchesNestedBranch()
    {
        Assert.True(
            RepositoryBranchPatternMatcher.Matches("release/1.0", "release/*", "main")
        );
    }

    [Fact]
    public void Matches_ExactName_IsCaseInsensitive()
    {
        Assert.True(RepositoryBranchPatternMatcher.Matches("Main", "main", null));
    }
}
