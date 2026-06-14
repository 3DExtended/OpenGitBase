using System.Net.Http.Json;
using OpenGitBase.Dispatcher.Models;
using OpenGitBase.Dispatcher.Options;

namespace OpenGitBase.Dispatcher.Services;

public sealed class RepositoryAccessCheckClient
{
    private readonly HttpClient _httpClient;
    private readonly DispatcherOptions _options;

    public RepositoryAccessCheckClient(HttpClient httpClient, Microsoft.Extensions.Options.IOptions<DispatcherOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<RepositoryAccessCheckResponse> CheckAsync(
        string publicKey,
        string repositoryPath,
        RepositoryOperation operation,
        CancellationToken cancellationToken
    )
    {
        var request = new RepositoryAccessCheckRequest
        {
            PublicKey = publicKey,
            RepositoryPath = repositoryPath,
            Operation = operation,
        };

        using var response = await _httpClient.PostAsJsonAsync(
            $"{_options.ApiUrl.TrimEnd('/')}/api/v1/access-checks/repositories",
            request,
            cancellationToken
        );

        response.EnsureSuccessStatusCode();

        var payload =
            await response.Content.ReadFromJsonAsync<RepositoryAccessCheckResponse>(
                cancellationToken: cancellationToken
            ) ?? throw new InvalidOperationException("Access check response was empty.");

        return payload;
    }
}
