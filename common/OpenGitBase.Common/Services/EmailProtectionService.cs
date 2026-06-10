using System.Security.Cryptography;
using System.Text;

using OpenGitBase.Common.Options;

namespace OpenGitBase.Common.Services;

public class EmailProtectionService : IEmailProtectionService
{
    private readonly byte[] _dataKey;

    private readonly byte[] _pepper;

    public EmailProtectionService(EncryptionOptions options)
    {
        _dataKey = Convert.FromBase64String(options.DataKey);
        _pepper = Encoding.UTF8.GetBytes(options.Pepper);

        if (_dataKey.Length != 32)
        {
            throw new InvalidOperationException("Encryption:DataKey must be a base64-encoded 32-byte key.");
        }
    }

    public string EncryptEmail(string email)
    {
        var plaintext = Encoding.UTF8.GetBytes(NormalizeEmail(email));
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(_dataKey, tagSizeInBytes: 16);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var payload = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, payload, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, payload, nonce.Length + tag.Length, ciphertext.Length);

        return Convert.ToBase64String(payload);
    }

    public string DecryptEmail(string ciphertext)
    {
        var payload = Convert.FromBase64String(ciphertext);
        var nonce = payload[..12];
        var tag = payload[12..28];
        var encrypted = payload[28..];

        var plaintext = new byte[encrypted.Length];
        using var aes = new AesGcm(_dataKey, tagSizeInBytes: 16);
        aes.Decrypt(nonce, encrypted, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }

    public string ComputeLookupHash(string email)
    {
        var normalized = NormalizeEmail(email);
        var hash = HMACSHA256.HashData(_pepper, Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
