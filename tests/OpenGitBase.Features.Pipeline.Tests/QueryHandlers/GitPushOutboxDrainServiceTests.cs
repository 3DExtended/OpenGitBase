using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Pipeline;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;
using OpenGitBase.Features.Pipeline.QueryHandlers;
using OpenGitBase.Features.Pipeline.Services;

namespace OpenGitBase.Features.Pipeline.Tests.QueryHandlers;

public class GitPushOutboxDrainServiceTests
{
    [Fact]
    public async Task Drain_MarksPendingProcessed_AndPublishesFanOut()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, PipelineMapsterConfig>(
            typeof(IngestGitPushQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        var repositoryId = Guid.NewGuid();
        await using (var seed = await scope.CreateDbContextAsync())
        {
            seed.Set<GitPushOutboxEntity>()
                .Add(
                    new GitPushOutboxEntity
                    {
                        Id = Guid.NewGuid(),
                        RepositoryId = repositoryId,
                        Ref = "refs/heads/main",
                        AfterSha = "cafe0001",
                        Status = GitPushOutboxStatus.Pending,
                        CreatedAt = DateTimeOffset.UtcNow,
                    }
                );
            await seed.SaveChangesAsync();
        }

        var publisher = new RecordingGitPushPublisher();
        var drain = new GitPushOutboxDrainService(
            scope.GetService<IDbContextFactory<OpenGitBaseDbContext>>(),
            new StubQueryProcessor(),
            publisher,
            NullLogger<GitPushOutboxDrainService>.Instance
        );

        var drained = await drain.DrainPendingAsync(CancellationToken.None);
        Assert.Equal(1, drained);
        Assert.Single(publisher.Calls);

        await using var context = await scope.CreateDbContextAsync();
        var row = await context.Set<GitPushOutboxEntity>().SingleAsync();
        Assert.Equal(GitPushOutboxStatus.Processed, row.Status);
        Assert.NotNull(row.ProcessedAt);
    }

    private sealed class StubQueryProcessor : IQueryProcessor
    {
        public Task<Option<TResult>> RunQueryAsync<TQuery, TResult>(
            IQuery<TResult, TQuery> query,
            CancellationToken cancellationToken
        )
            where TQuery : IQuery<TResult, TQuery> =>
            Task.FromResult(Option<TResult>.None);
    }

    private sealed class RecordingGitPushPublisher : IGitPushEventPublisher
    {
        public List<(Guid RepositoryId, string Ref, string AfterSha)> Calls { get; } = new();

        public Task PublishAsync(
            Guid repositoryId,
            string @ref,
            string afterSha,
            CancellationToken cancellationToken
        )
        {
            Calls.Add((repositoryId, @ref, afterSha));
            return Task.CompletedTask;
        }
    }
}
