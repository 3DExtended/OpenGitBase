using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenGitBase.Dispatcher.Options;

namespace OpenGitBase.Dispatcher.Tests;

public class SshSessionRunnerTests
{
    [Fact]
    public void ConfigureDispatcherServices_BindsTheFleetSelfRegistrationSettings()
    {
        var options = ResolveOptions(
            new Dictionary<string, string?>
            {
                ["Dispatcher:FleetSelfRegistrationEnabled"] = "false",
                ["Dispatcher:FleetHeartbeatIntervalSeconds"] = "45",
                ["Dispatcher:FleetProbeUrl"] = "http://dispatcher-7:8082/health",
            }
        );

        Assert.False(options.FleetSelfRegistrationEnabled);
        Assert.Equal(45, options.FleetHeartbeatIntervalSeconds);
        Assert.Equal("http://dispatcher-7:8082/health", options.FleetProbeUrl);
    }

    [Fact]
    public void ConfigureDispatcherServices_KeepsTheFleetDefaultsWhenUnset()
    {
        var options = ResolveOptions(new Dictionary<string, string?>());

        Assert.True(options.FleetSelfRegistrationEnabled);
        Assert.Equal(30, options.FleetHeartbeatIntervalSeconds);
        Assert.Null(options.FleetProbeUrl);
    }

    private static DispatcherOptions ResolveOptions(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        SshSessionRunner.ConfigureDispatcherServices(services, configuration);

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<DispatcherOptions>>().Value;
    }
}
