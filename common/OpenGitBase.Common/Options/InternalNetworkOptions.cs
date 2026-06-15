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
    ];
}
