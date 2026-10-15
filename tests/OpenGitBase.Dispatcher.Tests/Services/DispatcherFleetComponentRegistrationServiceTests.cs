using Microsoft.Extensions.Logging.Abstractions;
using OpenGitBase.Dispatcher.Options;
using OpenGitBase.Dispatcher.Services;

namespace OpenGitBase.Dispatcher.Tests.Services;

public class DispatcherFleetComponentRegistrationServiceTests
{
    [Fact]
    public async Task ExecuteAsync_KeepsRunningWhenTheApiIsUnreachableAtStartup()
    {
        var registrationAttempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var api = new HttpClient(new StubHandler(_ =>
        {
            registrationAttempted.TrySetResult();
            throw new HttpRequestException("Connection refused (api.test:9)");
        }));
        var options = Microsoft.Extensions.Options.Options.Create(
            new DispatcherOptions { ApiUrl = "http://api.test:9" }
        );
        using var service = new DispatcherFleetComponentRegistrationService(
            new FleetComponentRegistrationClient(api, options),
            options,
            NullLogger<DispatcherFleetComponentRegistrationService>.Instance
        );

        await service.StartAsync(CancellationToken.None);
        await registrationAttempted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        // A faulted ExecuteTask stops the host (BackgroundServiceExceptionBehavior.StopHost), so the
        // service must still be waiting for its first heartbeat tick once the registration has failed.
        await Task.WhenAny(service.ExecuteTask!, Task.Delay(TimeSpan.FromSeconds(1)));

        Assert.False(service.ExecuteTask!.IsCompleted, service.ExecuteTask.Exception?.ToString());
        await service.StopAsync(CancellationToken.None);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> answer)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => answer(request);
    }
}
