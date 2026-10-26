using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;
using OpenGitBase.Features.Pipeline.QueryHandlers;
using OpenGitBase.Features.Pipeline.Services;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Features.Pipeline.Tests.QueryHandlers;

public sealed class MaterializeWorkspaceArchiveQueryHandlerTests
{
    private const string CommitSha = "3f786850e387550fdab836ed7e6dc881de23001b";

    // Bare repositories live on the storage nodes, so this path never exists next to the API.
    private const string StoragePhysicalPath = "/srv/git/3e0e5f0e8f1a4a8c9d6b2f7a1c4e9b30.git";

    private static readonly byte[] Archive = [0x1f, 0x8b, 0x08, 0x00];

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task RunQueryAsync_ReturnsTheArchiveStorageBuiltAtThePushedCommit(int gitDepth)
    {
        var reader = FakeRepositoryArchiveReader.WithArchive(Archive);
        await using var scope = await PipelineHandlerTestScope.CreateAsync();
        var (handler, query, repositoryId) = await SeedJobAsync(scope, reader, CommitSha, gitDepth);

        var result = await handler.RunQueryAsync(query, CancellationToken.None);

        Assert.True(result.IsSome);
        Assert.Equal(Archive, result.Get().ArchiveBytes);
        Assert.Equal(gitDepth > 0, result.Get().IsShallow);
        Assert.Equal((repositoryId, StoragePhysicalPath, CommitSha, gitDepth), Assert.Single(reader.Reads));
    }

    [Fact]
    public async Task RunQueryAsync_WhenAfterShaIsNotAnObjectId_ReturnsNoneWithoutReadingStorage()
    {
        var reader = FakeRepositoryArchiveReader.WithArchive(Archive);
        await using var scope = await PipelineHandlerTestScope.CreateAsync();
        var (handler, query, _) = await SeedJobAsync(scope, reader, "HEAD", 0);

        var result = await handler.RunQueryAsync(query, CancellationToken.None);

        Assert.True(result.IsNone);
        Assert.Empty(reader.Reads);
    }

    // An outage is not an authentication failure: the caller answers it as unavailable, and the
    // agent fails the job instead of running its script in an empty workspace.
    [Fact]
    public async Task RunQueryAsync_WhenStorageCannotBuildTheArchive_Throws()
    {
        var reader = FakeRepositoryArchiveReader.Failing(
            new RepositoryFileUnavailableException("storage node unreachable")
        );
        await using var scope = await PipelineHandlerTestScope.CreateAsync();
        var (handler, query, _) = await SeedJobAsync(scope, reader, CommitSha, 0);

        await Assert.ThrowsAsync<RepositoryFileUnavailableException>(() =>
            handler.RunQueryAsync(query, CancellationToken.None)
        );
    }

    private static async Task<(MaterializeWorkspaceArchiveQueryHandler Handler, MaterializeWorkspaceArchiveQuery Query, Guid RepositoryId)> SeedJobAsync(
        PipelineHandlerTestScope scope,
        IRepositoryArchiveReader reader,
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
                    PhysicalPath = StoragePhysicalPath,
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

        var handler = new MaterializeWorkspaceArchiveQueryHandler(scope.ContextFactory, hasher, reader);
        var query = new MaterializeWorkspaceArchiveQuery
        {
            JobId = PipelineJobId.From(jobId),
            JobIdentityToken = token,
        };
        return (handler, query, repositoryId);
    }
}
