using OpenGitBase.Api.Services;

namespace OpenGitBase.Api.Tests.Services;

public class FleetSshKeyGeneratorTests
{
    [Fact]
    public void GenerateEd25519KeyPair_KeepsPrivateKeyTrailingNewline()
    {
        var (publicKey, privateKey) = new FleetSshKeyGenerator().GenerateEd25519KeyPair();

        Assert.StartsWith("ssh-ed25519 ", publicKey, StringComparison.Ordinal);
        Assert.DoesNotContain('\n', publicKey);
        Assert.StartsWith("-----BEGIN OPENSSH PRIVATE KEY-----\n", privateKey, StringComparison.Ordinal);
        Assert.EndsWith("-----END OPENSSH PRIVATE KEY-----\n", privateKey, StringComparison.Ordinal);
    }
}
