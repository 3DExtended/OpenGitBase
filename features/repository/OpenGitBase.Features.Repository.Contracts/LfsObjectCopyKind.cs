namespace OpenGitBase.Features.Repository.Contracts;

/// <summary>What a storage node holds of an LFS object.</summary>
public enum LfsObjectCopyKind
{
    /// <summary>The object itself, on the primary or a read replica.</summary>
    Plaintext = 0,

    /// <summary>A segmented AES-GCM copy under the Repository Key, on an encrypted replica.</summary>
    Encrypted = 1,
}
