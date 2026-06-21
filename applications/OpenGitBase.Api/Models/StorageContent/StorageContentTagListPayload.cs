namespace OpenGitBase.Api.Models.StorageContent;

public sealed class StorageContentTagListPayload
{
    public List<StorageContentRefPayload> Tags { get; init; } = [];
}
