using OpenGitBase.ComputeAgent;

namespace OpenGitBase.Api.Tests.ComputeAgent;

public class FirecrackerResourceLimitsTests
{
    [Fact]
    public void FromJob_MapsOgbHostedDefaults()
    {
        var limits = FirecrackerResourceLimits.FromJob(1, 2048, 20);
        Assert.Equal(1, limits.CpuLimit);
        Assert.Equal(2048, limits.MemoryMiB);
        Assert.Equal(20, limits.DiskGiB);
    }

    [Fact]
    public void FromEnvironment_ReadsJobSpecEnvVars()
    {
        var limits = FirecrackerResourceLimits.FromEnvironment(
            new Dictionary<string, string>
            {
                ["OGB_CPU_LIMIT"] = "2",
                ["OGB_MEMORY_MIB"] = "4096",
                ["OGB_DISK_GIB"] = "40",
            }
        );
        Assert.Equal(2, limits.CpuLimit);
        Assert.Equal(4096, limits.MemoryMiB);
        Assert.Equal(40, limits.DiskGiB);
    }
}
