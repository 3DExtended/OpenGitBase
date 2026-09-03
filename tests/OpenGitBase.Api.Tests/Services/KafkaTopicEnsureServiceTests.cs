using Microsoft.Extensions.Logging.Abstractions;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Options;

namespace OpenGitBase.Api.Tests.Services;

public class KafkaTopicEnsureServiceTests
{
    [Fact]
    public async Task StartAsync_NoOps_WhenBootstrapServersUnconfigured()
    {
        // With no BootstrapServers (tests / E2E), the service must not attempt to
        // build an admin client or connect — it simply returns.
        var service = new KafkaTopicEnsureService(
            new KafkaOptions { BootstrapServers = string.Empty },
            NullLogger<KafkaTopicEnsureService>.Instance
        );

        var start = service.StartAsync(CancellationToken.None);
        await start;

        Assert.True(start.IsCompletedSuccessfully);
    }

    [Fact]
    public void StopAsync_CompletesImmediately()
    {
        var service = new KafkaTopicEnsureService(
            new KafkaOptions { BootstrapServers = string.Empty },
            NullLogger<KafkaTopicEnsureService>.Instance
        );

        var stop = service.StopAsync(CancellationToken.None);

        Assert.True(stop.IsCompletedSuccessfully);
    }
}
