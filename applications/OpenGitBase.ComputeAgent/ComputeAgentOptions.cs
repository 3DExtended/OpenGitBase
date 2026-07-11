namespace OpenGitBase.ComputeAgent;

public sealed class ComputeAgentOptions
{
    public string ApiBaseUrl { get; set; } = "http://api-lb:8080";

    public string NodeId { get; set; } = "compute-agent-1";

    public string EnrollmentToken { get; set; } = string.Empty;

    public int HeartbeatSeconds { get; set; } = 15;

    public int ClaimPollSeconds { get; set; } = 5;

    public bool PreferProcessSandbox { get; set; } = true;
}
