namespace OpenGitBase.Api.Models.StorageContent;

public sealed class StorageContentRefsPayload
{
    public List<StorageContentRefPayload> Branches { get; init; } = [];

    public List<StorageContentRefPayload> Tags { get; init; } = [];

    public bool IsEmpty { get; init; }
}
