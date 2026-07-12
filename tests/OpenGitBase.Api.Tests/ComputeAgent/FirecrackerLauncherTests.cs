using OpenGitBase.ComputeAgent;

namespace OpenGitBase.Api.Tests.ComputeAgent;

public class FirecrackerLauncherTests
{
    [Fact]
    public void CanBootMicroVm_ReturnsFalseWhenKvmMissing()
    {
        if (File.Exists("/dev/kvm"))
        {
            return;
        }

        var launcher = new FirecrackerLauncher(new ComputeAgentOptions(), new HostEgressEnforcer());
        var canBoot = launcher.CanBootMicroVm(
            new FirecrackerLaunchRequest { RootFsPath = "/tmp" },
            out _,
            out var reason
        );

        Assert.False(canBoot);
        Assert.Equal("KVM unavailable", reason);
    }

    [Fact]
    public void CanBootMicroVm_ReturnsFalseWhenRootFsMissing()
    {
        var launcher = new FirecrackerLauncher(new ComputeAgentOptions(), new HostEgressEnforcer());
        var canBoot = launcher.CanBootMicroVm(
            new FirecrackerLaunchRequest(),
            out _,
            out var reason
        );

        Assert.False(canBoot);
        Assert.True(reason is "OGB_ROOTFS overlay root missing" or "KVM unavailable");
    }

    [Fact]
    public void ResourceLimits_FromJob_AppliesConservativeDefaults()
    {
        var limits = FirecrackerResourceLimits.FromJob(0, 0, 0);
        Assert.Equal(1, limits.CpuLimit);
        Assert.Equal(2048, limits.MemoryMiB);
        Assert.Equal(20, limits.DiskGiB);
    }

    [Fact]
    public void ResourceLimits_FromJob_UsesJobSpecValues()
    {
        var limits = FirecrackerResourceLimits.FromJob(2, 4096, 40);
        Assert.Equal(2, limits.CpuLimit);
        Assert.Equal(4096, limits.MemoryMiB);
        Assert.Equal(40, limits.DiskGiB);
    }
}
