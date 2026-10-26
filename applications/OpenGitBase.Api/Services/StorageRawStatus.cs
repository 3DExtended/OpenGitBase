namespace OpenGitBase.Api.Services;

public enum StorageRawStatus
{
    Ok,

    /// <summary>The blob is an LFS pointer to an object the repository never received.</summary>
    LfsObjectNotAvailable,

    /// <summary>The LFS object is recorded but no node holds it right now; a repair is queued.</summary>
    LfsObjectRestoring,
}
