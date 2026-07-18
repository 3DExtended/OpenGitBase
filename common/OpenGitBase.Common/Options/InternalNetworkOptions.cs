namespace OpenGitBase.Common.Options;

public class InternalNetworkOptions
{
    public bool Enabled { get; set; } = true;

    public string[] RestrictedPathPrefixes { get; set; } =
    [
        "/api/v1/storage-nodes/register",
        "/api/v1/storage-nodes/heartbeat",
        "/api/v1/storage-nodes/healthy",
        "/api/v1/storage-nodes/bootstrap",
        "/api/v1/fleet/bootstrap",
        "/api/v1/internal/fleet-components",
        "/api/v1/internal/repositories/push-validation",
        "/api/v1/internal/pipelines",
        "/internal/e2e",
    ];

    /// <summary>
    /// CIDR ranges for reverse proxies allowed to set X-Forwarded-For (Docker/HAProxy private networks).
    /// </summary>
    public string[] TrustedProxyNetworks { get; set; } =
    [
        "10.0.0.0/8",
        "172.16.0.0/12",
        "192.168.0.0/16",
    ];

    /// <summary>
    /// Individual proxy IP addresses (in addition to <see cref="TrustedProxyNetworks"/>).
    /// </summary>
    public string[] TrustedProxyAddresses { get; set; } = [];
}
