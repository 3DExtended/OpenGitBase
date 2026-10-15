using System.Text.Json;
using Confluent.Kafka;
using OpenGitBase.Common.Options;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Pipeline.Contracts;

namespace OpenGitBase.Api.Services;

public sealed class GitPushReceivedConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly KafkaOptions _kafkaOptions;
    private readonly ILogger<GitPushReceivedConsumer> _logger;

    public GitPushReceivedConsumer(
        IServiceProvider serviceProvider,
        KafkaOptions kafkaOptions,
        ILogger<GitPushReceivedConsumer> logger
    )
    {
        _serviceProvider = serviceProvider;
        _kafkaOptions = kafkaOptions;
        _logger = logger;
    }

    internal async Task ScheduleAsync(string messageValue, CancellationToken stoppingToken)
    {
        var payload = JsonSerializer.Deserialize<GitPushPayload>(messageValue);
        if (payload is null)
        {
            return;
        }

        try
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var queryProcessor = scope.ServiceProvider.GetRequiredService<IQueryProcessor>();
            await queryProcessor
                .RunQueryAsync(
                    new SchedulePipelineRunFromPushQuery
                    {
                        RepositoryId = payload.RepositoryId,
                        Ref = payload.Ref,
                        AfterSha = payload.AfterSha,
                    },
                    stoppingToken
                )
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The push outbox schedules durably and retries; this fan-out is best effort.
            _logger.LogWarning(
                ex,
                "Scheduling a pipeline for push {AfterSha} to repository {RepositoryId} failed.",
                payload.AfterSha,
                payload.RepositoryId
            );
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_kafkaOptions.BootstrapServers))
        {
            return;
        }

        var config = new ConsumerConfig
        {
            BootstrapServers = _kafkaOptions.BootstrapServers,
            GroupId = "opengitbase-pipeline-scheduler",
            AutoOffsetReset = AutoOffsetReset.Earliest,
        };
        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(_kafkaOptions.GitPushTopic);

        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? message;
            try
            {
                message = consumer.Consume(TimeSpan.FromSeconds(1));
            }
            catch (ConsumeException ex)
            {
                _logger.LogWarning(ex, "Failed to consume git push event.");
                continue;
            }

            if (message?.Message?.Value is null)
            {
                continue;
            }

            await ScheduleAsync(message.Message.Value, stoppingToken).ConfigureAwait(false);
        }
    }

    private sealed class GitPushPayload
    {
        public Guid RepositoryId { get; set; }

        public string Ref { get; set; } = string.Empty;

        public string AfterSha { get; set; } = string.Empty;
    }
}
