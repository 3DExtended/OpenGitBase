namespace OpenGitBase.Api.Services;

public sealed record ReplicationArtifactFetchResult(
    bool Success,
    int StatusCode,
    string ManifestJson,
    byte[] BundlePayload,
    string? Error = null
)
{
    public static ReplicationArtifactFetchResult Ok(string manifestJson, byte[] bundlePayload) =>
        new(true, 200, manifestJson, bundlePayload);

    public static ReplicationArtifactFetchResult Fail(int statusCode, string error) =>
        new(false, statusCode, string.Empty, [], error);
}
