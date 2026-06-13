namespace OpenGitBase.Cqrs.Tests.Core;

public class UnitTests
{
    [Fact]
    public void Value_IsAccessible()
    {
        Assert.Equal(default(Unit), Unit.Value);
    }
}
