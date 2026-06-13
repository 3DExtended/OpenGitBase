namespace OpenGitBase.Cqrs.Tests.Core;

public class NoneTests
{
    [Fact]
    public void ToString_ReturnsNone()
    {
        Assert.Equal("None", default(None).ToString());
    }
}
