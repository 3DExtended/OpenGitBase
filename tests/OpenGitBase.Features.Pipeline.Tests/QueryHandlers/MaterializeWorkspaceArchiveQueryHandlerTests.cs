using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;
using OpenGitBase.Features.Pipeline.QueryHandlers;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Features.Pipeline.Tests.QueryHandlers;

public sealed class MaterializeWorkspaceArchiveQueryHandlerTests
{
    private static readonly Dictionary<string, string> WorkspaceFiles = new()
    {
        ["README.md"] = "workspace\n",
    };

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task RunQueryAsync_WithCommitSha_ReturnsWorkspaceArchive(int gitDepth)
    {
        using var repository = await GitTestRepository.CreateAsync(WorkspaceFiles);
        await using var scope = await PipelineHandlerTestScope.CreateAsync();
        var (handler, query) = await SeedJobAsync(scope, repository.Path, repository.CommitSha, gitDepth);

        var result = await handler.RunQueryAsync(query, CancellationToken.None);

        Assert.True(result.IsSome);
        Assert.NotEmpty(result.Get().ArchiveBytes);
        Assert.Equal(gitDepth > 0, result.Get().IsShallow);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task RunQueryAsync_WhenAfterShaIsNotAnObjectId_ReturnsNone(int gitDepth)
    {
        using var repository = await GitTestRepository.CreateAsync(WorkspaceFiles);
        await using var scope = await PipelineHandlerTestScope.CreateAsync();
        var (handler, query) = await SeedJobAsync(scope, repository.Path, "HEAD", gitDepth);

        var result = await handler.RunQueryAsync(query, CancellationToken.None);

        Assert.True(result.IsNone);
    }

    private static async Task<(MaterializeWorkspaceArchiveQueryHandler Handler, MaterializeWorkspaceArchiveQuery Query)> SeedJobAsync(
        PipelineHandlerTestScope scope,
        string repositoryPath,
        string afterSha,
        int gitDepth
    )
    {
        var hasher = new PasswordHasherService();
        var repositoryId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var token = JobIdentityTokens.Mint(jobId);

        await using (var context = await scope.ContextFactory.CreateDbContextAsync())
        {
            context.Set<RepositoryEntity>().Add(
                new RepositoryEntity
                {
                    Id = repositoryId,
                    Name = "repo",
                    OwnerUserId = Guid.NewGuid(),
                    Slug = "repo",
                    PhysicalPath = repositoryPath,
                }
            );
            context.Set<PipelineRunEntity>().Add(
                new PipelineRunEntity
                {
                    Id = runId,
                    RepositoryId = repositoryId,
                    Ref = "main",
                    AfterSha = afterSha,
                    Status = PipelineRunStatus.Running,
                    CreatedAt = DateTimeOffset.UtcNow,
                }
            );
            context.Set<PipelineJobEntity>().Add(
                new PipelineJobEntity
                {
                    Id = jobId,
                    RunId = runId,
                    Name = "build",
                    Stage = "build",
                    RunsOn = "ogb-hosted",
                    Status = PipelineJobStatus.Running,
                    Script = "echo ok",
                    GitDepth = gitDepth,
                    CreatedAt = DateTimeOffset.UtcNow,
                }
            );
            context.Set<JobIdentityEntity>().Add(
                new JobIdentityEntity
                {
                    Id = Guid.NewGuid(),
                    JobId = jobId,
                    TokenHash = hasher.HashPassword(token),
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
                }
            );
            await context.SaveChangesAsync();
        }

        var handler = new MaterializeWorkspaceArchiveQueryHandler(scope.ContextFactory, hasher);
        var query = new MaterializeWorkspaceArchiveQuery
        {
            JobId = PipelineJobId.From(jobId),
            JobIdentityToken = token,
        };
        return (handler, query);
    }
}
