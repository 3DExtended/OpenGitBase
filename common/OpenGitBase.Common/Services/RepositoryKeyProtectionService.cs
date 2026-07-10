namespace OpenGitBase.Common.Services;

public class RepositoryKeyProtectionService : IRepositoryKeyProtectionService
{
    private readonly IEmailProtectionService _emailProtectionService;

    public RepositoryKeyProtectionService(IEmailProtectionService emailProtectionService)
    {
        _emailProtectionService = emailProtectionService;
    }

    public string ProtectKeyMaterial(byte[] keyMaterial) =>
        _emailProtectionService.EncryptSecret(Convert.ToBase64String(keyMaterial));

    public byte[] UnprotectKeyMaterial(string keyCiphertext) =>
        Convert.FromBase64String(_emailProtectionService.DecryptSecret(keyCiphertext));
}
