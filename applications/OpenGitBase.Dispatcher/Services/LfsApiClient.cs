using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using OpenGitBase.Dispatcher.Models;
using OpenGitBase.Dispatcher.Options;

namespace OpenGitBase.Dispatcher.Services;

/// <summary>Calls the API's internal LFS endpoints. Answers are returned whatever their status.</summary>
public sealed class LfsApiClient
{
    private readonly HttpClient _httpClient;
    private readonly DispatcherOptions _options;

    public LfsApiClient(HttpClient httpClient, IOptions<DispatcherOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public Task<LfsApiAnswer> BatchAsync(LfsBatchForwardRequest request, CancellationToken cancellationToken) =>
        PostAsync("batch", request, cancellationToken);

    public Task<LfsApiAnswer> CheckTransferAsync(
        LfsTransferCheckForwardRequest request,
        CancellationToken cancellationToken
    ) => PostAsync("transfer-check", request, cancellationToken);

    public Task<LfsApiAnswer> VerifyAsync(LfsVerifyForwardRequest request, CancellationToken cancellationToken) =>
        PostAsync("verify", request, cancellationToken);

    private async Task<LfsApiAnswer> PostAsync<T>(string endpoint, T payload, CancellationToken cancellationToken)
    {
        using var response = await _httpClient
            .PostAsJsonAsync($"{_options.ApiUrl.TrimEnd('/')}/api/v1/internal/lfs/{endpoint}", payload, cancellationToken)
            .ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return new LfsApiAnswer((int)response.StatusCode, body);
    }
}
