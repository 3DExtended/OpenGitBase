using System.Text;
using OpenGitBase.Features.AccountSecurity.Totp;

namespace OpenGitBase.Features.AccountSecurity.Tests.Totp;

public class TotpTests
{
    // RFC 6238 Appendix B, SHA1 seed. The RFC lists 8-digit values; the 6-digit codes are their last six digits.
    private static readonly byte[] RfcSecret = Encoding.ASCII.GetBytes("12345678901234567890");

    [Theory]
    [InlineData(59L, "287082")]
    [InlineData(1111111109L, "081804")]
    [InlineData(1111111111L, "050471")]
    [InlineData(1234567890L, "005924")]
    [InlineData(2000000000L, "279037")]
    [InlineData(20000000000L, "353130")]
    public void TryVerify_AcceptsRfc6238Sha1Vectors(long unixSeconds, string code)
    {
        var at = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);

        var accepted = TotpAlgorithm.TryVerify(RfcSecret, code, at, out var matchedStep);

        Assert.True(accepted);
        Assert.Equal(unixSeconds / 30, matchedStep);
    }

    [Theory]
    [InlineData(-30, true)]
    [InlineData(30, true)]
    [InlineData(-60, false)]
    [InlineData(60, false)]
    public void TryVerify_AcceptsOneStepOfClockDriftEitherWay(int driftSeconds, bool expected)
    {
        // The code shown at 1234567890 s, checked by a server whose clock is off by the drift.
        var serverTime = DateTimeOffset.FromUnixTimeSeconds(1234567890 + driftSeconds);

        var accepted = TotpAlgorithm.TryVerify(RfcSecret, "005924", serverTime, out var matchedStep);

        Assert.Equal(expected, accepted);
        if (expected)
        {
            Assert.Equal(1234567890L / 30, matchedStep);
        }
    }

    [Fact]
    public void GenerateSecret_Returns160RandomBitsAsUnpaddedBase32()
    {
        var first = TotpAlgorithm.GenerateSecret();
        var second = TotpAlgorithm.GenerateSecret();

        Assert.Equal(32, first.Length);
        Assert.Matches("^[A-Z2-7]{32}$", first);
        Assert.NotEqual(first, second);
        Assert.Equal(20, TotpAlgorithm.DecodeSecret(first).Length);
    }

    [Fact]
    public void DecodeSecret_ReadsTheRfcSecretFromBase32()
    {
        // "12345678901234567890" in RFC 4648 Base32, without padding.
        Assert.Equal(RfcSecret, TotpAlgorithm.DecodeSecret("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ"));
    }

    [Fact]
    public void BuildOtpauthUri_PrefixesTheLabelWithTheIssuerAndRepeatsItAsParameter()
    {
        var uri = TotpAlgorithm.BuildOtpauthUri("OpenGitBase", "alice", "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ");

        Assert.Equal(
            "otpauth://totp/OpenGitBase:alice?secret=GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ&issuer=OpenGitBase",
            uri
        );
    }

    [Fact]
    public void BuildOtpauthUri_EscapesIssuerAndAccountName()
    {
        var uri = TotpAlgorithm.BuildOtpauthUri("My Forge", "a.b+c", "GEZDGNBV");

        Assert.Equal("otpauth://totp/My%20Forge:a.b%2Bc?secret=GEZDGNBV&issuer=My%20Forge", uri);
    }

    [Theory]
    [InlineData("")]
    [InlineData("05924")]
    [InlineData("0059245")]
    [InlineData("00592a")]
    [InlineData(" 05924")]
    public void TryVerify_RejectsMalformedCodes(string code)
    {
        var at = DateTimeOffset.FromUnixTimeSeconds(1234567890);

        Assert.False(TotpAlgorithm.TryVerify(RfcSecret, code, at, out _));
    }
}
