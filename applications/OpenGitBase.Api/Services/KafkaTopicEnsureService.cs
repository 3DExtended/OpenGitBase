using Confluent.Kafka;
using Confluent.Kafka.Admin;
using OpenGitBase.Common.Options;

namespace OpenGitBase.Api.Services;

/// <summary>
/// Ensures the required Kafka application topics exist on API startup, idempotently.
///
/// The compose <c>kafka-init</c> one-shot only runs on a full cluster bring-up
/// (<c>service_completed_successfully</c>). If the brokers are later reset while the
/// API keeps running, the application topics silently vanish and every consume fails
/// with "Unknown topic or partition" — push events (CI, indexing) go inert with no
/// loud signal (issue #229). Ensuring the topics on every API start closes that gap:
/// any API (re)start re-creates missing topics, and a persistent failure is logged
/// loudly instead of being swallowed.
///
/// Implemented as <see cref="IHostedService"/> (not <see cref="BackgroundService"/>)
/// and registered before <see cref="GitPushReceivedConsumer"/> so the topics are
/// ensured before the consumer subscribes.
/// </summary>
public sealed class KafkaTopicEnsureService : IHostedService
{
    private const int MaxAttempts = 10;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan MetadataTimeout = TimeSpan.FromSeconds(10);

    private readonly KafkaOptions _options;
    private readonly ILogger<KafkaTopicEnsureService> _logger;

    public KafkaTopicEnsureService(KafkaOptions options, ILogger<KafkaTopicEnsureService> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.BootstrapServers))
        {
            // Kafka not configured (e.g. tests / E2E): nothing to ensure.
            return;
        }

        var requiredTopics = new[]
        {
            _options.GitPushTopic,
            _options.JobAvailableTopic,
            _options.JobCancelledTopic,
        };

        using var admin = new AdminClientBuilder(
            new AdminClientConfig { BootstrapServers = _options.BootstrapServers }
        ).Build();

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var missing = GetMissingTopics(admin, requiredTopics);
                if (missing.Count == 0)
                {
                    _logger.LogInformation(
                        "Kafka application topics present: {Topics}.",
                        string.Join(", ", requiredTopics)
                    );
                    return;
                }

                await admin
                    .CreateTopicsAsync(
                        missing.Select(name => new TopicSpecification
                        {
                            Name = name,
                            NumPartitions = _options.TopicPartitions,
                            ReplicationFactor = (short)_options.TopicReplicationFactor,
                        })
                    )
                    .ConfigureAwait(false);

                _logger.LogInformation(
                    "Created missing Kafka application topics: {Topics}.",
                    string.Join(", ", missing)
                );
                return;
            }
            catch (CreateTopicsException ex)
                when (ex.Results.All(result => result.Error.Code == ErrorCode.TopicAlreadyExists))
            {
                // Another API instance created them concurrently — the desired state holds.
                _logger.LogInformation("Kafka application topics already exist (created concurrently).");
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (attempt == MaxAttempts)
                {
                    _logger.LogError(
                        ex,
                        "Failed to ensure Kafka application topics after {Attempts} attempts. "
                            + "Push-event processing (CI triggers, indexing) will be inert until the "
                            + "topics exist. Required topics: {Topics}.",
                        MaxAttempts,
                        string.Join(", ", requiredTopics)
                    );
                    return;
                }

                _logger.LogWarning(
                    ex,
                    "Attempt {Attempt}/{Max} to ensure Kafka application topics failed; the cluster "
                        + "may still be forming quorum. Retrying in {Delay}s...",
                    attempt,
                    MaxAttempts,
                    RetryDelay.TotalSeconds
                );
                await Task.Delay(RetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static List<string> GetMissingTopics(
        IAdminClient admin,
        IReadOnlyCollection<string> requiredTopics
    )
    {
        // Metadata for ALL topics does not auto-create anything (unlike a metadata
        // request for a specific unknown topic), so it is a safe existence probe.
        var metadata = admin.GetMetadata(MetadataTimeout);
        var existing = metadata
            .Topics.Where(topic => topic.Error.Code == ErrorCode.NoError)
            .Select(topic => topic.Topic)
            .ToHashSet(StringComparer.Ordinal);

        return requiredTopics.Where(topic => !existing.Contains(topic)).ToList();
    }
}
