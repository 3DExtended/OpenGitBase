using OpenGitBase.Common.Services;

namespace OpenGitBase.Common.Tests.Services;

public class SshKeyFingerprintNormalizerTests
{
    [Fact]
    public void ToOpenSshSha256_PrefixesRawFingerprint()
    {
        Assert.Equal("SHA256:abc123", SshKeyFingerprintNormalizer.ToOpenSshSha256("abc123"));
    }

    [Theory]
    [InlineData("SHA256:abc", new[] { "SHA256:abc", "abc" })]
    [InlineData("abc", new[] { "abc", "SHA256:abc" })]
    public void GetLookupCandidates_ReturnsBothFormats(string input, string[] expected)
    {
        Assert.Equal(expected, SshKeyFingerprintNormalizer.GetLookupCandidates(input));
    }
}
