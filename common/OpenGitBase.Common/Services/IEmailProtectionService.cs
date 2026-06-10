namespace OpenGitBase.Common.Services;

public interface IEmailProtectionService
{
    string EncryptEmail(string email);

    string DecryptEmail(string ciphertext);

    string ComputeLookupHash(string email);
}
