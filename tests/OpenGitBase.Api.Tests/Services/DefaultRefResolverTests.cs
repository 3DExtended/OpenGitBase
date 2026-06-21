using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;

namespace OpenGitBase.Api.Tests.Services;

public class DefaultRefResolverTests
{
    [Fact]
    public void Resolve_WhenMainExists_ReturnsMain()
    {
        var branches = new List<RepositoryContentRefDto>
        {
            new() { Name = "develop", CommitSha = "abc" },
            new() { Name = "main", CommitSha = "def" },
            new() { Name = "master", CommitSha = "ghi" },
        };

        var result = DefaultRefResolver.Resolve(branches);

        Assert.Equal("main", result);
    }

    [Fact]
    public void Resolve_WhenMainMissingButMasterExists_ReturnsMaster()
    {
        var branches = new List<RepositoryContentRefDto>
        {
            new() { Name = "develop", CommitSha = "abc" },
            new() { Name = "master", CommitSha = "def" },
        };

        var result = DefaultRefResolver.Resolve(branches);

        Assert.Equal("master", result);
    }

    [Fact]
    public void Resolve_WhenMainAndMasterMissing_ReturnsFirstAlphabetically()
    {
        var branches = new List<RepositoryContentRefDto>
        {
            new() { Name = "release-2", CommitSha = "abc" },
            new() { Name = "develop", CommitSha = "def" },
            new() { Name = "feature-x", CommitSha = "ghi" },
        };

        var result = DefaultRefResolver.Resolve(branches);

        Assert.Equal("develop", result);
    }

    [Fact]
    public void Resolve_WhenMainUsesDifferentCasing_ReturnsMain()
    {
        var branches = new List<RepositoryContentRefDto>
        {
            new() { Name = "MAIN", CommitSha = "abc" },
        };

        var result = DefaultRefResolver.Resolve(branches);

        Assert.Equal("MAIN", result);
    }

    [Fact]
    public void Resolve_WhenEmpty_ReturnsNull()
    {
        var result = DefaultRefResolver.Resolve([]);

        Assert.Null(result);
    }
}
