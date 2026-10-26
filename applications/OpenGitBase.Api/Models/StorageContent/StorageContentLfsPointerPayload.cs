namespace OpenGitBase.Api.Models.StorageContent;

/// <summary>The LFS object a pointer blob names, as storage parsed it from the pointer.</summary>
public sealed class StorageContentLfsPointerPayload
{
    public string Oid { get; init; } = string.Empty;

    public long Size { get; init; }
}
