namespace OpenGitBase.Common.Options;

public class EncryptionOptions
{
    /// <summary>Base64-encoded 32-byte key for AES-256-GCM email encryption.</summary>
    public string DataKey { get; set; } = string.Empty;

    /// <summary>
    /// Earlier base64 data keys, only used to decrypt values written before a key rotation.
    /// New values are always encrypted with <see cref="DataKey"/>.
    /// </summary>
    public List<string> PreviousDataKeys { get; set; } = [];

    /// <summary>Secret used for HMAC-SHA256 email lookup hashes.</summary>
    public string Pepper { get; set; } = string.Empty;
}
