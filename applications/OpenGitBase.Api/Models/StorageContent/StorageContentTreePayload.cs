namespace OpenGitBase.Api.Models.StorageContent;

public sealed class StorageContentTreePayload
{
    public string Ref { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public List<StorageContentEntryPayload> Entries { get; init; } = [];
}
