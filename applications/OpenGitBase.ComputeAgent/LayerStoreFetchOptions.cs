namespace OpenGitBase.ComputeAgent;

public sealed class LayerStoreFetchOptions
{
    public string Endpoint { get; set; } = "http://minio:9000";

    public string Bucket { get; set; } = "opengitbase-layers";
}
