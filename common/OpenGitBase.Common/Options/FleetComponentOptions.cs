namespace OpenGitBase.Common.Options;

public sealed class FleetComponentOptions
{
    public string InstanceId { get; set; } = string.Empty;

    public string ProbeUrl { get; set; } = "http://127.0.0.1:8080/health";

    public int HeartbeatIntervalSeconds { get; set; } = 30;

    public int MissedHeartbeatThresholdSeconds { get; set; } = 90;

    public bool Enabled { get; set; } = true;

    public bool SelfRegistrationEnabled { get; set; } = true;
}
