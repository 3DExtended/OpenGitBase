namespace OpenGitBase.ComputeAgent;

public sealed class ComputeAgentOptions
{
    public string ApiBaseUrl { get; set; } = "http://api-lb:8080";

    public string NodeId { get; set; } = "compute-agent-1";

    public string EnrollmentToken { get; set; } = string.Empty;

    public int HeartbeatSeconds { get; set; } = 15;

    public int ClaimPollSeconds { get; set; } = 5;

    public bool PreferProcessSandbox { get; set; } = true;

    public string KernelPath { get; set; } = "/var/lib/opengitbase/vmlinux";

    public int GuestAgentVsockPort { get; set; } = 5000;

    public int GuestCid { get; set; } = 3;

    public IReadOnlyList<string> HostingProfiles { get; set; } =
    [
        "ogb-hosted",
        "organization-self-hosted",
        "community-hosted",
    ];
}
