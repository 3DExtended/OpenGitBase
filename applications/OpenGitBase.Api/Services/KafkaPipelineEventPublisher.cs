using System.Text.Json;
using Confluent.Kafka;
using OpenGitBase.Common.Options;
using OpenGitBase.Features.Pipeline.Services;

namespace OpenGitBase.Api.Services;

public sealed class KafkaPipelineEventPublisher : IGitPushEventPublisher, IJobAvailableEventPublisher
{
    private readonly KafkaOptions _options;
    private readonly ILogger<KafkaPipelineEventPublisher> _logger;

    public KafkaPipelineEventPublisher(
        KafkaOptions options,
        ILogger<KafkaPipelineEventPublisher> logger
    )
    {
        _options = options;
        _logger = logger;
    }

    public async Task PublishAsync(
        Guid repositoryId,
        string @ref,
        string afterSha,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(_options.BootstrapServers))
        {
            return;
        }

        var producerConfig = new ProducerConfig { BootstrapServers = _options.BootstrapServers };
        using var producer = new ProducerBuilder<string, string>(producerConfig).Build();
        var payload = JsonSerializer.Serialize(new { repositoryId, @ref, afterSha });
        await producer
            .ProduceAsync(
                _options.GitPushTopic,
                new Message<string, string> { Key = repositoryId.ToString(), Value = payload },
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    public async Task PublishAsync(Guid jobId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.BootstrapServers))
        {
            return;
        }

        var producerConfig = new ProducerConfig { BootstrapServers = _options.BootstrapServers };
        using var producer = new ProducerBuilder<string, string>(producerConfig).Build();
        await producer
            .ProduceAsync(
                _options.JobAvailableTopic,
                new Message<string, string> { Key = jobId.ToString(), Value = jobId.ToString() },
                cancellationToken
            )
            .ConfigureAwait(false);
        _logger.LogDebug("Published ci.job.available for {JobId}", jobId);
    }
}
