using System.Security.Cryptography;
using System.Text;

using OpenGitBase.Common.Options;

namespace OpenGitBase.Common.Services;

public class EmailProtectionService : IEmailProtectionService
{
    private readonly byte[] _dataKey;

    private readonly IReadOnlyList<byte[]> _previousDataKeys;

    private readonly byte[] _pepper;

    public EmailProtectionService(EncryptionOptions options)
    {
        _dataKey = ParseDataKey(options.DataKey, "Encryption:DataKey");
        _previousDataKeys = options.PreviousDataKeys
            .Select(key => ParseDataKey(key, "Encryption:PreviousDataKeys"))
            .ToList();
        _pepper = Encoding.UTF8.GetBytes(options.Pepper);
    }

    public string EncryptSecret(string value) => EncryptUtf8(value.Trim());

    public string DecryptSecret(string ciphertext) => DecryptUtf8(ciphertext);

    public string EncryptEmail(string email) => EncryptUtf8(NormalizeEmail(email));

    public string DecryptEmail(string ciphertext) => DecryptUtf8(ciphertext);

    public string ComputeLookupHash(string email)
    {
        var normalized = NormalizeEmail(email);
        var hash = HMACSHA256.HashData(_pepper, Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash);
    }

    public string Reprotect(string ciphertext)
    {
        var plaintext = DecryptBytes(ciphertext);
        var reprotected = EncryptBytes(plaintext);
        if (!DecryptWithKey(reprotected, _dataKey).AsSpan().SequenceEqual(plaintext))
        {
            throw new CryptographicException("Re-encrypted value does not match the original plaintext.");
        }

        return reprotected;
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static byte[] ParseDataKey(string base64Key, string settingName)
    {
        var key = Convert.FromBase64String(base64Key);
        if (key.Length != 32)
        {
            throw new InvalidOperationException($"{settingName} must be a base64-encoded 32-byte key.");
        }

        return key;
    }

    private static byte[] DecryptWithKey(string ciphertext, byte[] key)
    {
        var payload = Convert.FromBase64String(ciphertext);
        var nonce = payload[..12];
        var tag = payload[12..28];
        var encrypted = payload[28..];

        var plaintext = new byte[encrypted.Length];
        using var aes = new AesGcm(key, tagSizeInBytes: 16);
        aes.Decrypt(nonce, encrypted, tag, plaintext);

        return plaintext;
    }

    private string EncryptUtf8(string value) => EncryptBytes(Encoding.UTF8.GetBytes(value));

    private string EncryptBytes(byte[] plaintext)
    {
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

    private string DecryptUtf8(string ciphertext) => Encoding.UTF8.GetString(DecryptBytes(ciphertext));

    private byte[] DecryptBytes(string ciphertext)
    {
        try
        {
            return DecryptWithKey(ciphertext, _dataKey);
        }
        catch (AuthenticationTagMismatchException) when (_previousDataKeys.Count > 0)
        {
            foreach (var previousKey in _previousDataKeys)
            {
                try
                {
                    return DecryptWithKey(ciphertext, previousKey);
                }
                catch (AuthenticationTagMismatchException)
                {
                    // Written with a different earlier key; try the next one.
                }
            }

            throw;
        }
    }
}
