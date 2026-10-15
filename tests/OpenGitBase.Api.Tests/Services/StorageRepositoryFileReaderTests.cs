using System.Net;
using NSubstitute;
using OpenGitBase.Api.Models.StorageContent;
using OpenGitBase.Api.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Pipeline.Services;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Tests.Services;

public class StorageRepositoryFileReaderTests
{
    private const string PhysicalPath = "/srv/git/repo.git";
    private const string CommitSha = "3f786850e387550fdab836ed7e6dc881de23001b";
    private const string CiYaml = "build:\n  script: echo build\n";

    private static readonly Guid RepositoryGuid = Guid.NewGuid();

    private static readonly RepositoryRoutingTargetDto Primary = new()
    {
        StorageNodeId = Guid.NewGuid(),
        InternalHost = "storage-1",
        InternalHttpPort = 8081,
        IsPrimary = true,
        IsHealthy = true,
        IsInSync = true,
    };

    private static readonly RepositoryRoutingTargetDto Replica = new()
    {
        StorageNodeId = Guid.NewGuid(),
        InternalHost = "storage-2",
        InternalHttpPort = 8081,
        IsPrimary = false,
        IsHealthy = true,
        IsInSync = true,
    };

    private readonly IQueryProcessor _queryProcessor = Substitute.For<IQueryProcessor>();
    private readonly IStorageContentClient _storage = Substitute.For<IStorageContentClient>();

    public StorageRepositoryFileReaderTests()
    {
        GivenTargets(Replica, Primary);
        _queryProcessor
            .RunQueryAsync(Arg.Any<GetStorageNodeApiTokenQuery>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
                Option.From($"token-{callInfo.Arg<GetStorageNodeApiTokenQuery>().StorageNodeId.Value}")
            );
    }

    [Fact]
    public async Task ReadTextAsync_WhenFileExistsAtCommit_ReturnsItsTextFromThePrimary()
    {
        GivenTree(Primary, string.Empty, Blob(".opengitbase-ci.yml"), Blob("README.md"));
        GivenRaw(Primary, ".opengitbase-ci.yml", HttpStatusCode.OK, CiYaml);

        var text = await CreateReader().ReadTextAsync(
            RepositoryGuid,
            PhysicalPath,
            CommitSha,
            ".opengitbase-ci.yml",
            CancellationToken.None
        );

        Assert.Equal(CiYaml, text);
        await _storage
            .Received(1)
            .GetRawAsync(
                Primary,
                $"token-{Primary.StorageNodeId}",
                PhysicalPath,
                CommitSha,
                ".opengitbase-ci.yml",
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ReadTextAsync_WhenCommitHasNoSuchFile_ReturnsNullWithoutFetchingIt()
    {
        GivenTree(Primary, string.Empty, Blob("README.md"));

        var text = await CreateReader().ReadTextAsync(
            RepositoryGuid,
            PhysicalPath,
            CommitSha,
            ".opengitbase-ci.yml",
            CancellationToken.None
        );

        Assert.Null(text);
        await _storage
            .DidNotReceiveWithAnyArgs()
            .GetRawAsync(default!, default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task ReadTextAsync_WhenParentDirectoryIsMissing_ReturnsNull()
    {
        GivenTree(Primary, string.Empty, Blob("README.md"));

        var text = await CreateReader().ReadTextAsync(
            RepositoryGuid,
            PhysicalPath,
            CommitSha,
            "ci/pipeline.yml",
            CancellationToken.None
        );

        Assert.Null(text);
    }

    [Fact]
    public async Task ReadTextAsync_WhenFileIsNested_WalksDownToIt()
    {
        GivenTree(Primary, string.Empty, Tree("ci"));
        GivenTree(Primary, "ci", Blob("ci/pipeline.yml"));
        GivenRaw(Primary, "ci/pipeline.yml", HttpStatusCode.OK, CiYaml);

        var text = await CreateReader().ReadTextAsync(
            RepositoryGuid,
            PhysicalPath,
            CommitSha,
            "ci/pipeline.yml",
            CancellationToken.None
        );

        Assert.Equal(CiYaml, text);
    }

    [Fact]
    public Task ReadTextAsync_WhenStorageCannotListTheCommit_Throws()
    {
        // The storage node answers a missing commit (e.g. not replicated yet) and an outage
        // the same way, neither of which means the file is absent.
        GivenTree(Primary, string.Empty, entries: null);

        return Assert.ThrowsAsync<RepositoryFileUnavailableException>(() =>
            CreateReader().ReadTextAsync(
                RepositoryGuid,
                PhysicalPath,
                CommitSha,
                ".opengitbase-ci.yml",
                CancellationToken.None
            )
        );
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public Task ReadTextAsync_WhenFetchingTheListedFileFails_Throws(HttpStatusCode status)
    {
        GivenTree(Primary, string.Empty, Blob(".opengitbase-ci.yml"));
        GivenRaw(Primary, ".opengitbase-ci.yml", status, "{\"error\":\"boom\"}");

        return Assert.ThrowsAsync<RepositoryFileUnavailableException>(() =>
            CreateReader().ReadTextAsync(
                RepositoryGuid,
                PhysicalPath,
                CommitSha,
                ".opengitbase-ci.yml",
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task ReadTextAsync_WhenStorageNodeIsUnreachable_Throws()
    {
        _storage
            .GetTreeAsync(
                Arg.Any<RepositoryRoutingTargetDto>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns<StorageContentTreePayload?>(_ => throw new HttpRequestException("Connection refused"));

        var exception = await Assert.ThrowsAsync<RepositoryFileUnavailableException>(() =>
            CreateReader().ReadTextAsync(
                RepositoryGuid,
                PhysicalPath,
                CommitSha,
                ".opengitbase-ci.yml",
                CancellationToken.None
            )
        );
        Assert.IsType<HttpRequestException>(exception.InnerException);
    }

    [Fact]
    public async Task ReadTextAsync_WhenPrimaryIsUnhealthy_ReadsFromAHealthyReplica()
    {
        GivenTargets(new RepositoryRoutingTargetDto
        {
            StorageNodeId = Primary.StorageNodeId,
            InternalHost = Primary.InternalHost,
            InternalHttpPort = Primary.InternalHttpPort,
            IsPrimary = true,
            IsHealthy = false,
        }, Replica);
        GivenTree(Replica, string.Empty, Blob(".opengitbase-ci.yml"));
        GivenRaw(Replica, ".opengitbase-ci.yml", HttpStatusCode.OK, CiYaml);

        var text = await CreateReader().ReadTextAsync(
            RepositoryGuid,
            PhysicalPath,
            CommitSha,
            ".opengitbase-ci.yml",
            CancellationToken.None
        );

        Assert.Equal(CiYaml, text);
    }

    [Fact]
    public Task ReadTextAsync_WhenNoStorageNodeIsHealthy_Throws()
    {
        GivenTargets(new RepositoryRoutingTargetDto
        {
            StorageNodeId = Primary.StorageNodeId,
            IsPrimary = true,
            IsHealthy = false,
        });

        return Assert.ThrowsAsync<RepositoryFileUnavailableException>(() =>
            CreateReader().ReadTextAsync(
                RepositoryGuid,
                PhysicalPath,
                CommitSha,
                ".opengitbase-ci.yml",
                CancellationToken.None
            )
        );
    }

    [Fact]
    public Task ReadTextAsync_WhenRepositoryHasNoRouting_Throws()
    {
        _queryProcessor
            .RunQueryAsync(Arg.Any<RepositoryReplicationRoutingQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<RepositoryReplicationRoutingDto>.None);

        return Assert.ThrowsAsync<RepositoryFileUnavailableException>(() =>
            CreateReader().ReadTextAsync(
                RepositoryGuid,
                PhysicalPath,
                CommitSha,
                ".opengitbase-ci.yml",
                CancellationToken.None
            )
        );
    }

    private static StorageContentEntryPayload Blob(string path) =>
        new() { Name = path.Split('/')[^1], Path = path, Type = "blob" };

    private static StorageContentEntryPayload Tree(string path) =>
        new() { Name = path.Split('/')[^1], Path = path, Type = "tree" };

    private StorageRepositoryFileReader CreateReader() => new(_queryProcessor, _storage);

    private void GivenTargets(params RepositoryRoutingTargetDto[] targets) =>
        _queryProcessor
            .RunQueryAsync(
                Arg.Is<RepositoryReplicationRoutingQuery>(query => query.RepositoryId.Value == RepositoryGuid),
                Arg.Any<CancellationToken>()
            )
            .Returns(Option.From(new RepositoryReplicationRoutingDto { Targets = targets }));

    private void GivenTree(
        RepositoryRoutingTargetDto target,
        string directory,
        params StorageContentEntryPayload[]? entries
    ) =>
        _storage
            .GetTreeAsync(
                target,
                $"token-{target.StorageNodeId}",
                PhysicalPath,
                CommitSha,
                directory,
                Arg.Any<CancellationToken>()
            )
            .Returns(
                entries is null
                    ? null
                    : new StorageContentTreePayload
                    {
                        Ref = CommitSha,
                        Path = directory,
                        Entries = [.. entries],
                    }
            );

    private void GivenRaw(
        RepositoryRoutingTargetDto target,
        string path,
        HttpStatusCode status,
        string body
    ) =>
        _storage
            .GetRawAsync(
                target,
                $"token-{target.StorageNodeId}",
                PhysicalPath,
                CommitSha,
                path,
                Arg.Any<CancellationToken>()
            )
            .Returns(_ => new HttpResponseMessage(status) { Content = new StringContent(body) });
}
