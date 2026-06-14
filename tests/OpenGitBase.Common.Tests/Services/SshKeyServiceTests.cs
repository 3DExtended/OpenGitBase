using OpenGitBase.Common.Services;

namespace OpenGitBase.Common.Tests.Services;

public class SshKeyServiceTests
{
    private readonly SshKeyService _service = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateAndGetFingerprint_WhenEmpty_Throws(string? key)
    {
        var ex = Assert.Throws<ArgumentException>(() => _service.ValidateAndGetFingerprint(key));
        Assert.Equal("publicSshKey", ex.ParamName);
    }

    [Theory]
    [InlineData("invalid-key")]
    [InlineData("ecdsa-sha2-nistp256 AAAA")]
    public void ValidateAndGetFingerprint_WhenInvalidPrefix_Throws(string key)
    {
        Assert.Throws<ArgumentException>(() => _service.ValidateAndGetFingerprint(key));
    }

    [Fact]
    public void ValidateAndGetFingerprint_WhenMalformedBase64_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            _service.ValidateAndGetFingerprint("ssh-rsa not-valid-base64!!!")
        );
    }

    [Fact]
    public void ValidateAndGetFingerprint_WhenMissingKeyPart_Throws()
    {
        Assert.Throws<ArgumentException>(() => _service.ValidateAndGetFingerprint("ssh-rsa"));
    }

    [Fact]
    public void ValidateAndGetFingerprint_WhenValidRsaKey_ReturnsFingerprint()
    {
        const string key = "ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABgQC7";
        var fingerprint = _service.ValidateAndGetFingerprint(key);
        Assert.False(string.IsNullOrWhiteSpace(fingerprint));
        Assert.StartsWith(SshKeyFingerprintNormalizer.Sha256Prefix, fingerprint);
    }

    [Fact]
    public void ValidateAndGetFingerprint_WhenValidEd25519Key_ReturnsFingerprint()
    {
        const string key =
            "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIBsqn0bnF2207g75WsuF6spyWRQ0sN4T10bzcgk43r4=";
        var fingerprint = _service.ValidateAndGetFingerprint(key);
        Assert.False(string.IsNullOrWhiteSpace(fingerprint));
        Assert.StartsWith(SshKeyFingerprintNormalizer.Sha256Prefix, fingerprint);
    }
}
