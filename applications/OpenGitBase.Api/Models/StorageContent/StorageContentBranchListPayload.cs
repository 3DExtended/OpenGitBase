namespace OpenGitBase.Api.Models.StorageContent;

public sealed class StorageContentBranchListPayload
{
    public List<StorageContentRefPayload> Branches { get; init; } = [];
}
