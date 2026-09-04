namespace OpenGitBase.Api.Services;

/// <summary>
/// The true on-disk encrypted-artifact state of a storage node for one repository, as measured
/// by the node itself (the highest watermark for which a complete artifact is present on disk).
/// Distinct from the control-plane <c>ArtifactWatermark</c> column, which is only a cache.
/// </summary>
public sealed record ArtifactWatermarkStatusResult(
    bool Success,
    int StatusCode,
    long? ArtifactWatermark,
    string? Error = null
)
{
    public static ArtifactWatermarkStatusResult Ok(long? artifactWatermark) =>
        new(true, 200, artifactWatermark);

    public static ArtifactWatermarkStatusResult Fail(int statusCode, string error) =>
        new(false, statusCode, null, error);
}
