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
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Features.Pipeline.Tests.QueryHandlers;

public class GitPushOutboxDrainServiceTests
{
    private const string PushedSha = "3f786850e387550fdab836ed7e6dc881de23001b";

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

    [Fact]
    public async Task Drain_WhenCiFileIsOnStorage_SchedulesRunAndMarksProcessed()
    {
        await using var scope = await PipelineHandlerTestScope.CreateAsync();
        var repositoryId = await SeedRepositoryAsync(scope);
        await SeedOutboxRowAsync(scope, repositoryId);
        var reader = FakeRepositoryFileReader.WithFiles(
            new Dictionary<string, string>
            {
                [".opengitbase-ci.yml"] = """
                    image: alpine:latest
                    build:
                      stage: build
                      runs-on: ogb-hosted
                      script: echo build
                    """,
            }
        );

        var drained = await CreateDrain(scope, reader).DrainPendingAsync(CancellationToken.None);

        Assert.Equal(1, drained);
        await using var context = await scope.ContextFactory.CreateDbContextAsync();
        var run = await context.Set<PipelineRunEntity>().SingleAsync();
        Assert.Equal(PushedSha, run.AfterSha);
        var row = await context.Set<GitPushOutboxEntity>().SingleAsync();
        Assert.Equal(GitPushOutboxStatus.Processed, row.Status);
    }

    [Fact]
    public async Task Drain_WhenStorageCannotBeRead_LeavesRowPendingForRetry()
    {
        await using var scope = await PipelineHandlerTestScope.CreateAsync();
        var repositoryId = await SeedRepositoryAsync(scope);
        await SeedOutboxRowAsync(scope, repositoryId);
        var reader = FakeRepositoryFileReader.Failing(
            new RepositoryFileUnavailableException("No healthy storage node.")
        );

        var drained = await CreateDrain(scope, reader).DrainPendingAsync(CancellationToken.None);

        Assert.Equal(0, drained);
        await using var context = await scope.ContextFactory.CreateDbContextAsync();
        Assert.False(await context.Set<PipelineRunEntity>().AnyAsync());
        var row = await context.Set<GitPushOutboxEntity>().SingleAsync();
        Assert.Equal(GitPushOutboxStatus.Pending, row.Status);
        Assert.Equal(1, row.AttemptCount);
        Assert.Equal("No healthy storage node.", row.LastError);
    }

    private static GitPushOutboxDrainService CreateDrain(
        PipelineHandlerTestScope scope,
        IRepositoryFileReader reader
    ) =>
        new(
            scope.ContextFactory,
            new ScheduleHandlerQueryProcessor(
                new SchedulePipelineRunFromPushQueryHandler(
                    scope.ContextFactory,
                    scope.Mapper,
                    scope.JobPublisher,
                    reader
                )
            ),
            new RecordingGitPushPublisher(),
            NullLogger<GitPushOutboxDrainService>.Instance
        );

    private static async Task<Guid> SeedRepositoryAsync(PipelineHandlerTestScope scope)
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
                PhysicalPath = $"/srv/git/{repositoryId:N}.git",
            }
        );
        await context.SaveChangesAsync();
        return repositoryId;
    }

    private static async Task SeedOutboxRowAsync(PipelineHandlerTestScope scope, Guid repositoryId)
    {
        await using var context = await scope.ContextFactory.CreateDbContextAsync();
        context.Set<GitPushOutboxEntity>().Add(
            new GitPushOutboxEntity
            {
                Id = Guid.NewGuid(),
                RepositoryId = repositoryId,
                Ref = "refs/heads/main",
                AfterSha = PushedSha,
                Status = GitPushOutboxStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow,
            }
        );
        await context.SaveChangesAsync();
    }

    private sealed class ScheduleHandlerQueryProcessor(SchedulePipelineRunFromPushQueryHandler handler)
        : IQueryProcessor
    {
        public async Task<Option<TResult>> RunQueryAsync<TQuery, TResult>(
            IQuery<TResult, TQuery> query,
            CancellationToken cancellationToken
        )
            where TQuery : IQuery<TResult, TQuery>
        {
            var result = await handler.RunQueryAsync(
                (SchedulePipelineRunFromPushQuery)(object)query,
                cancellationToken
            );
            return (Option<TResult>)(object)result;
        }
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
