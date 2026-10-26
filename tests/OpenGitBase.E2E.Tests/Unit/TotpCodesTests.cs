using OpenGitBase.E2E.Core;

namespace OpenGitBase.E2E.Tests.Unit;

[Trait("Category", "E2EUnit")]
public class TotpCodesTests
{
    // RFC 6238 Appendix B, SHA1: the ASCII secret "12345678901234567890", last 6 digits.
    private const string RfcSecret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

    [Theory]
    [InlineData(59L, "287082")]
    [InlineData(1111111109L, "081804")]
    [InlineData(20000000000L, "353130")]
    public void ForStep_MatchesTheRfcVectors(long unixSeconds, string expected) =>
        Assert.Equal(expected, TotpCodes.ForStep(RfcSecret, TotpCodes.StepAt(DateTimeOffset.FromUnixTimeSeconds(unixSeconds))));
}
