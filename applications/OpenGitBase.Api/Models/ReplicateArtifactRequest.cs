using System.Text.Json;

namespace OpenGitBase.Api.Models;

public sealed class ReplicateArtifactRequest
{
    public Guid TargetStorageNodeId { get; init; }

    public long Watermark { get; init; }

    public JsonElement Manifest { get; init; }

    /// <summary>Hex-encoded encrypted bundle payload (named for wire compatibility).</summary>
    public string BundleBase64 { get; init; } = string.Empty;
}
