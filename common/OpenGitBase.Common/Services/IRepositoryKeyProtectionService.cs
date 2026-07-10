namespace OpenGitBase.Common.Services;

public interface IRepositoryKeyProtectionService
{
    string ProtectKeyMaterial(byte[] keyMaterial);

    byte[] UnprotectKeyMaterial(string keyCiphertext);
}
