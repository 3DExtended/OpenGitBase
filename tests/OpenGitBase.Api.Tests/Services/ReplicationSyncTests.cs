using OpenGitBase.Api.Services;

namespace OpenGitBase.Api.Tests.Services;

public class ReplicationSyncTests
{
    [Theory]
    [InlineData(3, 3, true)]
    [InlineData(2, 3, false)]
    [InlineData(4, 3, true)]
    public void IsInSync_ComparesAppliedAndPrimaryWatermarks(
        long applied,
        long primary,
        bool expected
    )
    {
        Assert.Equal(expected, ReplicationSync.IsInSync(applied, primary));
    }
}
