namespace OpenGitBase.Api.Models.StorageContent;

public sealed class StorageContentBlobPayload
{
    public string Ref { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public long Size { get; init; }

    public bool IsBinary { get; init; }

    public bool IsTooLarge { get; init; }

    public string PreviewKind { get; init; } = "text";

    public string? TextContent { get; init; }

    public string? ContentBase64 { get; init; }
}
