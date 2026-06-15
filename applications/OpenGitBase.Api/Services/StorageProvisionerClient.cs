using System.Net.Http.Headers;
using System.Net.Http.Json;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Services;

public sealed class StorageProvisionerClient : IStorageProvisionerClient
{
    private readonly HttpClient _httpClient;

    public StorageProvisionerClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<StorageProvisionerResult> ProvisionRepositoryAsync(
        StorageNodeDto node,
        string apiToken,
        string physicalPath,
        CancellationToken cancellationToken
    ) =>
        SendAsync(
            HttpMethod.Post,
            node,
            apiToken,
            physicalPath,
            successStatusCodes: [201],
            cancellationToken
        );

    public Task<StorageProvisionerResult> DeleteRepositoryAsync(
        StorageNodeDto node,
        string apiToken,
        string physicalPath,
        CancellationToken cancellationToken
    ) =>
        SendAsync(
            HttpMethod.Delete,
            node,
            apiToken,
            physicalPath,
            successStatusCodes: [200],
            cancellationToken
        );

    private async Task<StorageProvisionerResult> SendAsync(
        HttpMethod method,
        StorageNodeDto node,
        string apiToken,
        string physicalPath,
        int[] successStatusCodes,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(apiToken))
        {
            return StorageProvisionerResult.Fail(401, "Storage node API token is missing.");
        }

        var requestUri =
            $"http://{node.InternalHost}:{node.InternalHttpPort}/internal/repos";
        using var request = new HttpRequestMessage(method, requestUri)
        {
            Content = JsonContent.Create(new { physicalPath }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

        using var response = await _httpClient
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (successStatusCodes.Contains((int)response.StatusCode))
        {
            return StorageProvisionerResult.Ok((int)response.StatusCode);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var error = string.IsNullOrWhiteSpace(body)
            ? $"Storage request failed with status {(int)response.StatusCode}."
            : body;

        return StorageProvisionerResult.Fail((int)response.StatusCode, error);
    }
}
