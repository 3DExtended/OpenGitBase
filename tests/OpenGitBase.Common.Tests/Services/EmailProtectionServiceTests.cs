using System.Security.Cryptography;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Services;

namespace OpenGitBase.Common.Tests.Services;

public class EmailProtectionServiceTests
{
    private static readonly string PreviousDataKey = Convert.ToBase64String(new byte[32]);

    private static readonly string CurrentDataKey = Convert.ToBase64String(
        Enumerable.Repeat((byte)7, 32).ToArray()
    );

    [Fact]
    public void Constructor_WhenDataKeyInvalidLength_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new EmailProtectionService(
                new EncryptionOptions
                {
                    DataKey = Convert.ToBase64String(new byte[16]),
                    Pepper = "pepper",
                }
            )
        );
        Assert.Contains("32-byte", ex.Message);
    }

    [Fact]
    public void EncryptDecrypt_RoundTripsNormalizedEmail()
    {
        var service = CreateService();
        var ciphertext = service.EncryptEmail("  User@Example.COM ");
        var decrypted = service.DecryptEmail(ciphertext);
        Assert.Equal("user@example.com", decrypted);
    }

    [Fact]
    public void ComputeLookupHash_IsDeterministicAndCaseInsensitive()
    {
        var service = CreateService();
        var first = service.ComputeLookupHash("User@Example.com");
        var second = service.ComputeLookupHash("  user@example.com  ");
        Assert.Equal(first, second);
        Assert.False(string.IsNullOrWhiteSpace(first));
    }

    [Fact]
    public void EncryptDecryptSecret_PreservesCaseSensitiveValue()
    {
        var service = CreateService();
        var value = "AbC+/MixedCaseToken==";
        var ciphertext = service.EncryptSecret(value);
        var decrypted = service.DecryptSecret(ciphertext);
        Assert.Equal(value, decrypted);
    }

    [Fact]
    public void DecryptSecret_WithCiphertextFromAPreviousDataKey_ReturnsPlaintext()
    {
        var ciphertext = CreateService(PreviousDataKey).EncryptSecret("AbC+/MixedCaseToken==");

        var decrypted = CreateService(CurrentDataKey, PreviousDataKey).DecryptSecret(ciphertext);

        Assert.Equal("AbC+/MixedCaseToken==", decrypted);
    }

    [Fact]
    public void DecryptSecret_WithCiphertextFromAnUnknownDataKey_Throws()
    {
        var ciphertext = CreateService(PreviousDataKey).EncryptSecret("value");

        Assert.ThrowsAny<CryptographicException>(() =>
            CreateService(CurrentDataKey).DecryptSecret(ciphertext)
        );
    }

    [Fact]
    public void Reprotect_MovesCiphertextToTheCurrentDataKeyWithoutChangingThePlaintext()
    {
        var ciphertext = CreateService(PreviousDataKey).EncryptEmail("User@Example.com");

        var reprotected = CreateService(CurrentDataKey, PreviousDataKey).Reprotect(ciphertext);

        Assert.Equal("user@example.com", CreateService(CurrentDataKey).DecryptEmail(reprotected));
        Assert.ThrowsAny<CryptographicException>(() =>
            CreateService(PreviousDataKey).DecryptEmail(reprotected)
        );
    }

    [Fact]
    public void Reprotect_KeepsTheExactStoredBytes()
    {
        const string value = "  Mixed Case\nwith whitespace \n";
        var ciphertext = CreateService(PreviousDataKey).EncryptSecret(value);
        var original = CreateService(PreviousDataKey).DecryptSecret(ciphertext);

        var reprotected = CreateService(CurrentDataKey, PreviousDataKey).Reprotect(ciphertext);

        Assert.Equal(original, CreateService(CurrentDataKey).DecryptSecret(reprotected));
    }

    private static EmailProtectionService CreateService(string dataKey, params string[] previousDataKeys) =>
        new(
            new EncryptionOptions
            {
                DataKey = dataKey,
                PreviousDataKeys = [.. previousDataKeys],
                Pepper = "test-pepper",
            }
        );

    private static EmailProtectionService CreateService() =>
        new(
            new EncryptionOptions
            {
                DataKey = Convert.ToBase64String(new byte[32]),
                Pepper = "test-pepper",
            }
        );
}
