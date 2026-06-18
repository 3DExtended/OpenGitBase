using OpenGitBase.Common.Options;
using OpenGitBase.Common.Services;

namespace OpenGitBase.Common.Tests.Services;

public class EmailProtectionServiceTests
{
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

    private static EmailProtectionService CreateService() =>
        new(
            new EncryptionOptions
            {
                DataKey = Convert.ToBase64String(new byte[32]),
                Pepper = "test-pepper",
            }
        );
}
