namespace OpenGitBase.Common.Services;

public interface IEmailProtectionService
{
    string EncryptEmail(string email);

    string DecryptEmail(string ciphertext);

    string EncryptSecret(string value);

    string DecryptSecret(string ciphertext);

    string ComputeLookupHash(string email);

    /// <summary>Re-encrypts a stored ciphertext with the current data key, keeping its plaintext byte for byte.</summary>
    string Reprotect(string ciphertext);
}
