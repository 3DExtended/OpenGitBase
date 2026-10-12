using Microsoft.EntityFrameworkCore;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;
using OpenGitBase.Features.Pipeline.QueryHandlers;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Features.Pipeline.Tests.QueryHandlers;

public sealed class SchedulePipelineRunFromPushQueryHandlerTests
{
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
        using var repository = await GitTestRepository.CreateAsync(CiFiles);
        await using var scope = await PipelineHandlerTestScope.CreateAsync();
        var repositoryId = await SeedRepositoryAsync(scope, repository.Path);

        var result = await CreateHandler(scope).RunQueryAsync(
            new SchedulePipelineRunFromPushQuery
            {
                RepositoryId = repositoryId,
                Ref = "refs/heads/main",
                AfterSha = repository.CommitSha,
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        await using var context = await scope.ContextFactory.CreateDbContextAsync();
        var run = await context.Set<PipelineRunEntity>().SingleAsync();
        Assert.Equal(repository.CommitSha, run.AfterSha);
    }

    [Theory]
    [InlineData("HEAD")]
    [InlineData("main")]
    public async Task RunQueryAsync_WhenAfterShaIsNotAnObjectId_SchedulesNothing(string afterSha)
    {
        using var repository = await GitTestRepository.CreateAsync(CiFiles);
        await using var scope = await PipelineHandlerTestScope.CreateAsync();
        var repositoryId = await SeedRepositoryAsync(scope, repository.Path);

        var result = await CreateHandler(scope).RunQueryAsync(
            new SchedulePipelineRunFromPushQuery
            {
                RepositoryId = repositoryId,
                Ref = "refs/heads/main",
                AfterSha = afterSha,
            },
            CancellationToken.None
        );

        Assert.True(result.IsNone);
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
        using var repository = await GitTestRepository.CreateAsync(
            new Dictionary<string, string> { [".opengitbase-ci.yml"] = pipelineYaml }
        );
        await using var scope = await PipelineHandlerTestScope.CreateAsync();
        var repositoryId = await SeedRepositoryAsync(scope, repository.Path);

        var result = await CreateHandler(scope).RunQueryAsync(
            new SchedulePipelineRunFromPushQuery
            {
                RepositoryId = repositoryId,
                Ref = "refs/heads/main",
                AfterSha = repository.CommitSha,
            },
            CancellationToken.None
        );

        Assert.True(result.IsNone);
        await using var context = await scope.ContextFactory.CreateDbContextAsync();
        Assert.False(await context.Set<PipelineJobEntity>().AnyAsync());
    }

    private static SchedulePipelineRunFromPushQueryHandler CreateHandler(PipelineHandlerTestScope scope) =>
        new(scope.ContextFactory, scope.Mapper, scope.JobPublisher);

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
