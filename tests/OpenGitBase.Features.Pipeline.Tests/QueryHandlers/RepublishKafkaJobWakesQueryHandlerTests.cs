using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.Pipeline;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;
using OpenGitBase.Features.Pipeline.QueryHandlers;
using OpenGitBase.Features.Pipeline.Services;

namespace OpenGitBase.Features.Pipeline.Tests.QueryHandlers;

public class RepublishKafkaJobWakesQueryHandlerTests
{
    [Fact]
    public async Task Republish_PublishesAvailableForQueuedJobs()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, PipelineMapsterConfig>(
            typeof(RepublishKafkaJobWakesQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        var queuedJobId = Guid.NewGuid();
        await using (var seed = await scope.CreateDbContextAsync())
        {
            seed.Set<PipelineJobEntity>()
                .Add(
                    new PipelineJobEntity
                    {
                        Id = queuedJobId,
                        RunId = Guid.NewGuid(),
                        Name = "build",
                        Stage = "build",
                        Status = PipelineJobStatus.Queued,
                        CreatedAt = DateTimeOffset.UtcNow,
                    }
                );
            await seed.SaveChangesAsync();
        }

        var available = new RecordingAvailablePublisher();
        var cancelled = new RecordingCancelledPublisher();
        var handler = new RepublishKafkaJobWakesQueryHandler(
            scope.GetService<IDbContextFactory<OpenGitBaseDbContext>>(),
            available,
            cancelled
        );

        var result = await handler.RunQueryAsync(
            new RepublishKafkaJobWakesQuery(),
            CancellationToken.None
        );

        var value = QueryHandlerResultAssert.AssertSome(result);
        Assert.Equal(1, value.QueuedJobWakes);
        Assert.Contains(queuedJobId, available.Calls);
    }

    private sealed class RecordingAvailablePublisher : IJobAvailableEventPublisher
    {
        public List<Guid> Calls { get; } = new();

        public Task PublishAsync(Guid jobId, CancellationToken cancellationToken)
        {
            Calls.Add(jobId);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingCancelledPublisher : IJobCancelledEventPublisher
    {
        public List<Guid> Calls { get; } = new();

        public Task PublishCancelledAsync(Guid jobId, CancellationToken cancellationToken)
        {
            Calls.Add(jobId);
            return Task.CompletedTask;
        }
    }
}
