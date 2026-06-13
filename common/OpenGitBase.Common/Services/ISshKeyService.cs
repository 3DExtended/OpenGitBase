namespace OpenGitBase.Common.Services;

public interface ISshKeyService
{
    /// <summary>
    /// Validates the provided SSH public key and returns its fingerprint if valid.
    /// </summary>
    /// <param name="publicSshKey">The SSH public key to validate.</param>
    /// <returns>The fingerprint of the SSH key if valid; otherwise, null.</returns>
    string? ValidateAndGetFingerprint(string? publicSshKey);
}
