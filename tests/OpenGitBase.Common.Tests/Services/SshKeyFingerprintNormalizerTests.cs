using OpenGitBase.Common.Services;

namespace OpenGitBase.Common.Tests.Services;

public class SshKeyFingerprintNormalizerTests
{
    private const string Ed25519Key =
        "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIKPvQqOddAJDo6coJxqZRE5ryuDdyce1e/S2u8azZ0X4";

    private const string OpenSshFingerprint = "SHA256:gLmfXwUJ5fQIiHymKjrCfvoVYALr91myqq7XduS52f4";

    private const string DotNetFingerprint =
        "SHA256:gLmfXwUJ5fQIiHymKjrCfvoVYALr91myqq7XduS52f4=";

    [Fact]
    public void ToOpenSshSha256_StripsBase64Padding()
    {
        Assert.Equal(
            OpenSshFingerprint,
            SshKeyFingerprintNormalizer.ToOpenSshSha256("gLmfXwUJ5fQIiHymKjrCfvoVYALr91myqq7XduS52f4=")
        );
    }

    [Fact]
    public void ValidateAndGetFingerprint_MatchesOpenSshFormat()
    {
        var service = new SshKeyService();
        Assert.Equal(OpenSshFingerprint, service.ValidateAndGetFingerprint(Ed25519Key));
    }

    [Fact]
    public void GetLookupCandidates_WhenOpenSshFormat_IncludesLegacyPaddedVariant()
    {
        var candidates = SshKeyFingerprintNormalizer.GetLookupCandidates(OpenSshFingerprint);

        Assert.Contains(OpenSshFingerprint, candidates);
        Assert.Contains(DotNetFingerprint, candidates);
    }

    [Fact]
    public void GetLookupCandidates_WhenLegacyPaddedStored_IncludesOpenSshVariant()
    {
        var candidates = SshKeyFingerprintNormalizer.GetLookupCandidates(DotNetFingerprint);

        Assert.Contains(OpenSshFingerprint, candidates);
        Assert.Contains(DotNetFingerprint, candidates);
    }

    [Theory]
    [InlineData("SHA256:")]
    [InlineData("SHA256:   ")]
    public void GetLookupCandidates_WhenOnlyPrefixProvided_ReturnsTrimmedInput(string input)
    {
        var candidates = SshKeyFingerprintNormalizer.GetLookupCandidates(input);

        Assert.Equal([input.Trim()], candidates);
    }

    [Fact]
    public void GetLookupCandidates_WhenUnpaddedLengthIsMultipleOfFour_DoesNotAddPaddingVariants()
    {
        var candidates = SshKeyFingerprintNormalizer.GetLookupCandidates("SHA256:abcd");

        Assert.Equal(["SHA256:abcd", "abcd"], candidates.ToArray());
    }

    [Theory]
    [InlineData("SHA256:abc", new[] { "SHA256:abc", "abc" })]
    [InlineData("abc", new[] { "abc", "SHA256:abc" })]
    public void GetLookupCandidates_ReturnsPrefixedAndRawFormats(string input, string[] expected)
    {
        var candidates = SshKeyFingerprintNormalizer.GetLookupCandidates(input);
        foreach (var value in expected)
        {
            Assert.Contains(value, candidates);
        }
    }
}
