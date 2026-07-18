using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.Pipeline;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;
using OpenGitBase.Features.Pipeline.QueryHandlers;

namespace OpenGitBase.Features.Pipeline.Tests.QueryHandlers;

public class IngestGitPushOutboxTests
{
    [Fact]
    public async Task Ingest_WritesPendingOutbox_WithoutRequiringKafka()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, PipelineMapsterConfig>(
            typeof(IngestGitPushQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        var handler = scope.GetHandler<IngestGitPushQueryHandler>();
        var repositoryId = Guid.NewGuid();
        var result = await handler.RunQueryAsync(
            new IngestGitPushQuery
            {
                RepositoryId = repositoryId,
                Ref = "refs/heads/main",
                AfterSha = "abc123def456",
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);

        await using var context = await scope.CreateDbContextAsync();
        var row = await context.Set<GitPushOutboxEntity>().SingleAsync();
        Assert.Equal(repositoryId, row.RepositoryId);
        Assert.Equal("refs/heads/main", row.Ref);
        Assert.Equal("abc123def456", row.AfterSha);
        Assert.Equal(GitPushOutboxStatus.Pending, row.Status);
    }

    [Fact]
    public async Task Ingest_DuplicateRepositoryAndSha_IsIdempotent()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, PipelineMapsterConfig>(
            typeof(IngestGitPushQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        var handler = scope.GetHandler<IngestGitPushQueryHandler>();
        var query = new IngestGitPushQuery
        {
            RepositoryId = Guid.NewGuid(),
            Ref = "refs/heads/main",
            AfterSha = "deadbeef",
        };

        Assert.True((await handler.RunQueryAsync(query, CancellationToken.None)).IsSome);
        Assert.True((await handler.RunQueryAsync(query, CancellationToken.None)).IsSome);

        await using var context = await scope.CreateDbContextAsync();
        Assert.Equal(1, await context.Set<GitPushOutboxEntity>().CountAsync());
    }
}
