using System.Net;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Features.Status.Contracts;
using OpenGitBase.Features.Status.Services;

namespace OpenGitBase.Features.Status.Tests.Services;

public class StatusProbeEngineTests
{
    [Fact]
    public async Task ProbeHttpAsync_SuccessUnderThreshold_ReturnsHealthy()
    {
        var engine = CreateEngine(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var snapshot = await engine.ProbeHttpAsync(
            "api-1",
            "http://example.test/health",
            timeoutMs: 5000,
            slowThresholdMs: 2000,
            CancellationToken.None
        );

        Assert.Equal(PublicHealthStatus.Healthy, snapshot.Status);
        Assert.Equal("api-1", snapshot.InstanceId);
    }

    [Fact]
    public async Task ProbeHttpAsync_NonSuccessStatus_ReturnsUnhealthy()
    {
        var engine = CreateEngine(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        var snapshot = await engine.ProbeHttpAsync(
            "api-1",
            "http://example.test/health",
            timeoutMs: 5000,
            slowThresholdMs: 2000,
            CancellationToken.None
        );

        Assert.Equal(PublicHealthStatus.Unhealthy, snapshot.Status);
        Assert.Contains("503", snapshot.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProbeHttpAsync_SlowResponse_ReturnsDegraded()
    {
        var engine = CreateEngine(async _ =>
        {
            await Task.Delay(2100).ConfigureAwait(false);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var snapshot = await engine.ProbeHttpAsync(
            "web-1",
            "http://example.test/health",
            timeoutMs: 5000,
            slowThresholdMs: 2000,
            CancellationToken.None
        );

        Assert.Equal(PublicHealthStatus.Degraded, snapshot.Status);
    }

    private static StatusProbeEngine CreateEngine(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler
    )
    {
        var services = new ServiceCollection();
        services
            .AddHttpClient(nameof(StatusProbeEngine))
            .ConfigurePrimaryHttpMessageHandler(
                () => new StubHttpMessageHandler(handler)
            );
        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        return new StatusProbeEngine(factory);
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler
    ) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => handler(request);
    }
}
