using OpenGitBase.Common.Services;

namespace OpenGitBase.Common.Tests.Services;

public class SystemClockTests
{
    [Fact]
    public void UtcNow_ReturnsCurrentUtcTime()
    {
        var before = DateTimeOffset.UtcNow;
        var clock = new SystemClock();
        var after = DateTimeOffset.UtcNow;
        Assert.InRange(clock.UtcNow, before, after.AddSeconds(1));
    }
}
