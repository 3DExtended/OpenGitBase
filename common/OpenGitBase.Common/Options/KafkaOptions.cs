namespace OpenGitBase.Common.Options;

public sealed class KafkaOptions
{
    public string BootstrapServers { get; set; } = string.Empty;

    public string GitPushTopic { get; set; } = "git.push.received";

    public string JobAvailableTopic { get; set; } = "ci.job.available";
}
