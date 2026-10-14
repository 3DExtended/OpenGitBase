namespace OpenGitBase.Dispatcher.Options;

public class DispatcherOptions
{
    public string ApiUrl { get; set; } = "http://api:8080";

    public string DispatcherId { get; set; } = "dispatcher-1";

    public int HttpPort { get; set; } = 8082;

    /// <summary>
    /// Largest git push accepted over HTTP, in bytes. Replaces Kestrel's 30 MB request body default for
    /// receive-pack only. Defaults to the default repository quota (1 GiB): a larger push could not fit anyway.
    /// </summary>
    public long MaxPushBytes { get; set; } = 1_073_741_824;

    public string StorageSshPrivateKeyPath { get; set; } = "/run/secrets/storage_ssh_key";

    public string StorageSshUser { get; set; } = "git";

    public int StorageSshConnectTimeoutSeconds { get; set; } = 30;

    public bool FleetSelfRegistrationEnabled { get; set; } = true;

    public string? FleetProbeUrl { get; set; }

    public int FleetHeartbeatIntervalSeconds { get; set; } = 30;
}
