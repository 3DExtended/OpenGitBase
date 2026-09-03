namespace OpenGitBase.Common.Options;

public sealed class KafkaOptions
{
    public string BootstrapServers { get; set; } = string.Empty;

    public string GitPushTopic { get; set; } = "git.push.received";

    public string JobAvailableTopic { get; set; } = "ci.job.available";

    public string JobCancelledTopic { get; set; } = "ci.job.cancelled";

    /// <summary>
    /// Partition count used when the API ensures the application topics exist on
    /// startup. Matches docker/kafka/bootstrap-topics.sh.
    /// </summary>
    public int TopicPartitions { get; set; } = 3;

    /// <summary>
    /// Replication factor used when the API ensures the application topics exist
    /// on startup. Must not exceed the broker count. Matches bootstrap-topics.sh.
    /// </summary>
    public int TopicReplicationFactor { get; set; } = 3;
}
