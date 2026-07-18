namespace OpenGitBase.Features.Pipeline.Contracts;

public sealed class RepublishKafkaJobWakesResult
{
    public int QueuedJobWakes { get; set; }

    public int CancelledJobWakes { get; set; }
}
