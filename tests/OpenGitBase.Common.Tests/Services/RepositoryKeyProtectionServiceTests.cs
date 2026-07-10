using OpenGitBase.Common.Options;
using OpenGitBase.Common.Services;

namespace OpenGitBase.Common.Tests.Services;

public class RepositoryKeyProtectionServiceTests
{
    [Fact]
    public void ProtectUnprotect_RoundTripsKeyMaterial()
    {
        var service = CreateService();
        var keyMaterial = Convert.FromBase64String(
            Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
        );

        var protectedValue = service.ProtectKeyMaterial(keyMaterial);
        var roundTripped = service.UnprotectKeyMaterial(protectedValue);

        Assert.Equal(keyMaterial, roundTripped);
    }

    private static RepositoryKeyProtectionService CreateService() =>
        new(
            new EmailProtectionService(
                new EncryptionOptions
                {
                    DataKey = Convert.ToBase64String(new byte[32]),
                    Pepper = "test-pepper",
                }
            )
        );
}
