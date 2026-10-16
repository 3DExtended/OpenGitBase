using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;
using OpenGitBase.Features.Pipeline.QueryHandlers;
using OpenGitBase.Features.Pipeline.Services;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Features.Pipeline.Tests.QueryHandlers;

public sealed class SchedulePipelineRunFromPushQueryHandlerTests
{
    private const string CommitSha = "3f786850e387550fdab836ed7e6dc881de23001b";

    // Bare repositories live on the storage nodes, so this path never exists next to the API.
    private const string StoragePhysicalPath = "/srv/git/3e0e5f0e8f1a4a8c9d6b2f7a1c4e9b30.git";

    private static readonly Dictionary<string, string> CiFiles = new()
    {
        [".opengitbase-ci.yml"] = """
            image: alpine:latest
            build:
              stage: build
              runs-on: ogb-hosted
              script: echo build
            """,
    };

    [Fact]
    public async Task RunQueryAsync_WithCommitSha_SchedulesRun()
    {
        var reader = FakeRepositoryFileReader.WithFiles(CiFiles);
        await using var scope = await PipelineHandlerTestScope.CreateAsync();
        var repositoryId = await SeedRepositoryAsync(scope, StoragePhysicalPath);

        var result = await CreateHandler(scope, reader).RunQueryAsync(
            new SchedulePipelineRunFromPushQuery
            {
                RepositoryId = repositoryId,
                Ref = "refs/heads/main",
                AfterSha = CommitSha,
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        await using var context = await scope.ContextFactory.CreateDbContextAsync();
        var run = await context.Set<PipelineRunEntity>().SingleAsync();
        Assert.Equal(CommitSha, run.AfterSha);
        Assert.Single(await context.Set<PipelineJobEntity>().ToListAsync());
    }

    [Fact]
    public async Task RunQueryAsync_ReadsCiFileAtPushedCommitThroughRepositoryFileReader()
    {
        var reader = FakeRepositoryFileReader.WithFiles(CiFiles);
        await using var scope = await PipelineHandlerTestScope.CreateAsync();
        var repositoryId = await SeedRepositoryAsync(scope, StoragePhysicalPath);

        await CreateHandler(scope, reader).RunQueryAsync(
            new SchedulePipelineRunFromPushQuery
            {
                RepositoryId = repositoryId,
                Ref = "refs/heads/main",
                AfterSha = CommitSha,
            },
            CancellationToken.None
        );

        var read = Assert.Single(reader.Reads);
        Assert.Equal(repositoryId, read.RepositoryId);
        Assert.Equal(StoragePhysicalPath, read.PhysicalPath);
        Assert.Equal(CommitSha, read.CommitSha);
        Assert.Equal(".opengitbase-ci.yml", read.Path);
    }

    [Fact]
    public async Task RunQueryAsync_WhenCommitHasNoCiFile_SchedulesNothing()
    {
        var reader = FakeRepositoryFileReader.WithFiles(new Dictionary<string, string>
        {
            ["README.md"] = "# repo",
        });
        await using var scope = await PipelineHandlerTestScope.CreateAsync();
        var repositoryId = await SeedRepositoryAsync(scope, StoragePhysicalPath);

        var result = await CreateHandler(scope, reader).RunQueryAsync(
            new SchedulePipelineRunFromPushQuery
            {
                RepositoryId = repositoryId,
                Ref = "refs/heads/main",
                AfterSha = CommitSha,
            },
            CancellationToken.None
        );

        Assert.True(result.IsNone);
        await using var context = await scope.ContextFactory.CreateDbContextAsync();
        Assert.False(await context.Set<PipelineRunEntity>().AnyAsync());
    }

    [Fact]
    public async Task RunQueryAsync_WhenStorageCannotBeRead_ThrowsSoThePushIsRetried()
    {
        var reader = FakeRepositoryFileReader.Failing(
            new RepositoryFileUnavailableException("storage node unreachable")
        );
        await using var scope = await PipelineHandlerTestScope.CreateAsync();
        var repositoryId = await SeedRepositoryAsync(scope, StoragePhysicalPath);

        await Assert.ThrowsAsync<RepositoryFileUnavailableException>(() =>
            CreateHandler(scope, reader).RunQueryAsync(
                new SchedulePipelineRunFromPushQuery
                {
                    RepositoryId = repositoryId,
                    Ref = "refs/heads/main",
                    AfterSha = CommitSha,
                },
                CancellationToken.None
            )
        );

        await using var context = await scope.ContextFactory.CreateDbContextAsync();
        Assert.False(await context.Set<PipelineRunEntity>().AnyAsync());
    }

    [Theory]
    [InlineData("0000000000000000000000000000000000000000")]
    [InlineData("0000000000000000000000000000000000000000000000000000000000000000")]
    public async Task RunQueryAsync_WhenPushDeletedTheRef_SchedulesNothingWithoutReadingStorage(
        string afterSha
    )
    {
        var reader = FakeRepositoryFileReader.WithFiles(CiFiles);
        await using var scope = await PipelineHandlerTestScope.CreateAsync();
        var repositoryId = await SeedRepositoryAsync(scope, StoragePhysicalPath);

        var result = await CreateHandler(scope, reader).RunQueryAsync(
            new SchedulePipelineRunFromPushQuery
            {
                RepositoryId = repositoryId,
                Ref = "refs/heads/feature",
                AfterSha = afterSha,
            },
            CancellationToken.None
        );

        Assert.True(result.IsNone);
        Assert.Empty(reader.Reads);
    }

    [Theory]
    [InlineData("HEAD")]
    [InlineData("main")]
    public async Task RunQueryAsync_WhenAfterShaIsNotAnObjectId_SchedulesNothing(string afterSha)
    {
        var reader = FakeRepositoryFileReader.WithFiles(CiFiles);
        await using var scope = await PipelineHandlerTestScope.CreateAsync();
        var repositoryId = await SeedRepositoryAsync(scope, StoragePhysicalPath);

        var result = await CreateHandler(scope, reader).RunQueryAsync(
            new SchedulePipelineRunFromPushQuery
            {
                RepositoryId = repositoryId,
                Ref = "refs/heads/main",
                AfterSha = afterSha,
            },
            CancellationToken.None
        );

        Assert.True(result.IsNone);
        Assert.Empty(reader.Reads);
        await using var context = await scope.ContextFactory.CreateDbContextAsync();
        Assert.False(await context.Set<PipelineRunEntity>().AnyAsync());
    }

    [Theory]
    [InlineData("""
        image: alpine:latest
        variables:
          OGB_LAYER_PROMOTION_REQUEST_ID: 6f1d2c1e-0000-4000-8000-000000000001
        build:
          stage: build
          runs-on: ogb-hosted
          script: echo build
        """)]
    [InlineData("""
        image: alpine:latest
        build:
          stage: build
          runs-on: ogb-hosted
          script: echo build
          variables:
            OGB_OVERLAY_UPPER: /tmp
        """)]
    public async Task RunQueryAsync_WhenCiFileSetsPlatformVariables_SchedulesNothing(string pipelineYaml)
    {
        var reader = FakeRepositoryFileReader.WithFiles(
            new Dictionary<string, string> { [".opengitbase-ci.yml"] = pipelineYaml }
        );
        await using var scope = await PipelineHandlerTestScope.CreateAsync();
        var repositoryId = await SeedRepositoryAsync(scope, StoragePhysicalPath);

        var result = await CreateHandler(scope, reader).RunQueryAsync(
            new SchedulePipelineRunFromPushQuery
            {
                RepositoryId = repositoryId,
                Ref = "refs/heads/main",
                AfterSha = CommitSha,
            },
            CancellationToken.None
        );

        Assert.True(result.IsNone);
        await using var context = await scope.ContextFactory.CreateDbContextAsync();
        Assert.False(await context.Set<PipelineJobEntity>().AnyAsync());
    }

    // A push reports the full ref (refs/heads/main), while `only` patterns name the branch or tag,
    // e.g. `main` or `release/*`, as the CI docs promise.
    [Theory]
    [InlineData("refs/heads/main", "main", true)]
    [InlineData("refs/heads/release/1.2", "release/*", true)]
    [InlineData("refs/tags/v1.0.0", "v*", true)]
    [InlineData("refs/heads/feature", "main", false)]
    [InlineData("refs/heads/main", "refs/heads/main", false)]
    public async Task RunQueryAsync_MatchesOnlyAgainstTheBranchOrTagName(
        string pushedRef,
        string pattern,
        bool expectJob
    )
    {
        var reader = FakeRepositoryFileReader.WithFiles(
            new Dictionary<string, string>
            {
                [".opengitbase-ci.yml"] = $$"""
                    image: alpine:latest
                    build:
                      stage: build
                      runs-on: ogb-hosted
                      only:
                        - "{{pattern}}"
                      script: echo build
                    """,
            }
        );
        await using var scope = await PipelineHandlerTestScope.CreateAsync();
        var repositoryId = await SeedRepositoryAsync(scope, StoragePhysicalPath);

        await CreateHandler(scope, reader).RunQueryAsync(
            new SchedulePipelineRunFromPushQuery
            {
                RepositoryId = repositoryId,
                Ref = pushedRef,
                AfterSha = CommitSha,
            },
            CancellationToken.None
        );

        await using var context = await scope.ContextFactory.CreateDbContextAsync();
        Assert.Equal(expectJob, await context.Set<PipelineJobEntity>().AnyAsync());
    }

    [Theory]
    [InlineData("refs/heads/release/1.2", "release/1.2")]
    [InlineData("refs/tags/v1.0.0", "v1.0.0")]
    public async Task RunQueryAsync_SetsCommitRefNameToTheBranchOrTagName(string pushedRef, string expected)
    {
        var reader = FakeRepositoryFileReader.WithFiles(CiFiles);
        await using var scope = await PipelineHandlerTestScope.CreateAsync();
        var repositoryId = await SeedRepositoryAsync(scope, StoragePhysicalPath);

        await CreateHandler(scope, reader).RunQueryAsync(
            new SchedulePipelineRunFromPushQuery
            {
                RepositoryId = repositoryId,
                Ref = pushedRef,
                AfterSha = CommitSha,
            },
            CancellationToken.None
        );

        await using var context = await scope.ContextFactory.CreateDbContextAsync();
        var job = await context.Set<PipelineJobEntity>().SingleAsync();
        var environment = JsonSerializer.Deserialize<Dictionary<string, string>>(job.EnvironmentJson)!;
        Assert.Equal(expected, environment["CI_COMMIT_REF_NAME"]);
        Assert.Equal(pushedRef, (await context.Set<PipelineRunEntity>().SingleAsync()).Ref);
    }

    private static SchedulePipelineRunFromPushQueryHandler CreateHandler(
        PipelineHandlerTestScope scope,
        IRepositoryFileReader reader
    ) =>
        new(scope.ContextFactory, scope.Mapper, scope.JobPublisher, reader);

    private static async Task<Guid> SeedRepositoryAsync(
        PipelineHandlerTestScope scope,
        string physicalPath
    )
    {
        var repositoryId = Guid.NewGuid();
        await using var context = await scope.ContextFactory.CreateDbContextAsync();
        context.Set<RepositoryEntity>().Add(
            new RepositoryEntity
            {
                Id = repositoryId,
                Name = "repo",
                OwnerUserId = Guid.NewGuid(),
                Slug = "repo",
                PhysicalPath = physicalPath,
            }
        );
        await context.SaveChangesAsync();
        return repositoryId;
    }
}
