namespace OpenGitBase.Common.Options;

public class EncryptionOptions
{
    /// <summary>Base64-encoded 32-byte key for AES-256-GCM email encryption.</summary>
    public string DataKey { get; set; } = string.Empty;

    /// <summary>Secret used for HMAC-SHA256 email lookup hashes.</summary>
    public string Pepper { get; set; } = string.Empty;
}
