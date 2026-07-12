using OpenGitBase.ComputeAgent;

namespace OpenGitBase.Api.Tests.ComputeAgent;

public class HostEgressEnforcerTests
{
    [Fact]
    public async Task ValidateDomainAsync_DeniesUnknownDomain()
    {
        var enforcer = new HostEgressEnforcer();
        var result = await enforcer.ValidateDomainAsync(
            "blocked.example",
            ["registry.npmjs.org"],
            CancellationToken.None
        );

        Assert.False(result.Allowed);
        Assert.Contains("Domain Allowance Request", result.DenialLogLine, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateDomainAsync_AllowsListedDomain()
    {
        var enforcer = new HostEgressEnforcer();
        var result = await enforcer.ValidateDomainAsync(
            "registry.npmjs.org",
            ["registry.npmjs.org"],
            CancellationToken.None
        );

        Assert.True(result.Allowed);
    }
}
