using OpenGitBase.Common.Security;

namespace OpenGitBase.Common.Tests.Security;

public class NodeCertificateThumbprintTests
{
    [Theory]
    [InlineData("AA:BB:CC", "AABBCC")]
    [InlineData("aa bb cc", "AABBCC")]
    public void Normalize_RemovesSeparatorsAndUppercases(string input, string expected)
    {
        Assert.Equal(expected, NodeCertificateThumbprint.Normalize(input));
    }

    [Fact]
    public void Matches_ComparesNormalizedValues()
    {
        Assert.True(
            NodeCertificateThumbprint.Matches(
                "AA:BB:CC",
                "aabbcc"
            )
        );
    }
}
