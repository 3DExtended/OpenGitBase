using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Options;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Services;

namespace OpenGitBase.Api.Tests.Services;

public class GitPushReceivedConsumerTests
{
    [Fact]
    public async Task ScheduleAsync_WhenSchedulingFails_DoesNotStopTheConsumer()
    {
        // An exception escaping a BackgroundService stops the whole API host; the push outbox
        // already retries scheduling durably, so the consumer only logs.
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<SchedulePipelineRunFromPushQuery>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new RepositoryFileUnavailableException("No healthy storage node."));
        var services = new ServiceCollection().AddSingleton(queryProcessor).BuildServiceProvider();
        var consumer = new GitPushReceivedConsumer(
            services,
            new KafkaOptions(),
            NullLogger<GitPushReceivedConsumer>.Instance
        );
        var repositoryId = Guid.NewGuid();

        await consumer.ScheduleAsync(
            $$"""{"RepositoryId":"{{repositoryId}}","Ref":"refs/heads/main","AfterSha":"3f786850e387550fdab836ed7e6dc881de23001b"}""",
            CancellationToken.None
        );

        await queryProcessor
            .Received(1)
            .RunQueryAsync(
                Arg.Is<SchedulePipelineRunFromPushQuery>(query =>
                    query.RepositoryId == repositoryId
                    && query.AfterSha == "3f786850e387550fdab836ed7e6dc881de23001b"
                ),
                Arg.Any<CancellationToken>()
            );
    }
}
