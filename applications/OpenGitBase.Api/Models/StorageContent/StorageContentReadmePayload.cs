namespace OpenGitBase.Api.Models.StorageContent;

public sealed class StorageContentReadmePayload
{
    public string Ref { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public string MarkdownSource { get; init; } = string.Empty;
}
