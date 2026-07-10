using System.Net;
using System.Net.Http.Json;
using OpenGitBase.Dispatcher.Options;

namespace OpenGitBase.Dispatcher.Services;

public sealed class FleetComponentRegistrationClient
{
    private readonly HttpClient _httpClient;
    private readonly DispatcherOptions _options;

    public FleetComponentRegistrationClient(
        HttpClient httpClient,
        Microsoft.Extensions.Options.IOptions<DispatcherOptions> options
    )
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<HttpStatusCode> RegisterAsync(
        string instanceId,
        string probeUrl,
        CancellationToken cancellationToken
    )
    {
        using var response = await _httpClient
            .PostAsJsonAsync(
                $"{_options.ApiUrl.TrimEnd('/')}/api/v1/internal/fleet-components/register",
                new
                {
                    componentType = "Git",
                    instanceId,
                    probeUrl,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return response.StatusCode;
    }

    public async Task<HttpStatusCode> HeartbeatAsync(
        string instanceId,
        CancellationToken cancellationToken
    )
    {
        using var response = await _httpClient
            .PostAsJsonAsync(
                $"{_options.ApiUrl.TrimEnd('/')}/api/v1/internal/fleet-components/heartbeat",
                new
                {
                    componentType = "Git",
                    instanceId,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return response.StatusCode;
    }
}
