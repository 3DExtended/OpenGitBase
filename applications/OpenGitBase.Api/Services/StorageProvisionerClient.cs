using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
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
        long receiveMaxBytes,
        string replicationRole = "Primary",
        CancellationToken cancellationToken = default
    ) =>
        SendAsync(
            HttpMethod.Post,
            node,
            apiToken,
            physicalPath,
            receiveMaxBytes,
            replicationRole,
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
            receiveMaxBytes: null,
            replicationRole: "Primary",
            successStatusCodes: [200],
            cancellationToken
        );

    public Task<StorageProvisionerResult> SyncRepositoryFromPeerAsync(
        StorageNodeDto node,
        string apiToken,
        string physicalPath,
        string sourceHost,
        string sourcePhysicalPath,
        int sourcePort,
        CancellationToken cancellationToken
    ) =>
        SendSyncFromAsync(
            node,
            apiToken,
            physicalPath,
            sourceHost,
            sourcePhysicalPath,
            sourcePort,
            cancellationToken
        );

    public async Task<StorageProvisionerResult> UploadReplicationArtifactAsync(
        StorageNodeDto node,
        string apiToken,
        Guid repositoryId,
        long watermark,
        string manifestJson,
        byte[] bundlePayload,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(apiToken))
        {
            return StorageProvisionerResult.Fail(401, "Storage node API token is missing.");
        }

        var requestUri =
            $"http://{node.InternalHost}:{node.InternalHttpPort}/internal/repos/{repositoryId:D}/artifacts/{watermark}";
        using var manifestDocument = JsonDocument.Parse(manifestJson);
        var payload = JsonSerializer.Serialize(
            new
            {
                manifest = manifestDocument.RootElement,
                bundleBase64 = Convert.ToHexString(bundlePayload).ToLowerInvariant(),
            }
        );
        using var request = new HttpRequestMessage(HttpMethod.Put, requestUri)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

        using var response = await _httpClient
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if ((int)response.StatusCode is 200 or 201)
        {
            return StorageProvisionerResult.Ok((int)response.StatusCode);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return StorageProvisionerResult.Fail(
            (int)response.StatusCode,
            string.IsNullOrWhiteSpace(body)
                ? $"Artifact upload failed with status {(int)response.StatusCode}."
                : body
        );
    }

    public async Task<ReplicationArtifactFetchResult> TryGetReplicationArtifactAsync(
        StorageNodeDto node,
        string apiToken,
        Guid repositoryId,
        long watermark,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(apiToken))
        {
            return ReplicationArtifactFetchResult.Fail(401, "Storage node API token is missing.");
        }

        var requestUri =
            $"http://{node.InternalHost}:{node.InternalHttpPort}/internal/repos/{repositoryId:D}/artifacts/{watermark}";
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

        using var response = await _httpClient
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if ((int)response.StatusCode != 200)
        {
            return ReplicationArtifactFetchResult.Fail(
                (int)response.StatusCode,
                string.IsNullOrWhiteSpace(body)
                    ? $"Artifact fetch failed with status {(int)response.StatusCode}."
                    : body
            );
        }

        using var document = JsonDocument.Parse(body);
        var manifestJson = document.RootElement.GetProperty("manifest").GetRawText();
        var bundleHex = document.RootElement.GetProperty("bundleBase64").GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(bundleHex))
        {
            return ReplicationArtifactFetchResult.Fail(502, "Artifact response missing bundle payload.");
        }

        return ReplicationArtifactFetchResult.Ok(manifestJson, Convert.FromHexString(bundleHex));
    }

    public async Task<StorageProvisionerResult> DeleteReplicationArtifactAsync(
        StorageNodeDto node,
        string apiToken,
        Guid repositoryId,
        long watermark,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(apiToken))
        {
            return StorageProvisionerResult.Fail(401, "Storage node API token is missing.");
        }

        var requestUri =
            $"http://{node.InternalHost}:{node.InternalHttpPort}/internal/repos/{repositoryId:D}/artifacts/{watermark}";
        using var request = new HttpRequestMessage(HttpMethod.Delete, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

        using var response = await _httpClient
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if ((int)response.StatusCode is 200 or 204 or 404)
        {
            return StorageProvisionerResult.Ok((int)response.StatusCode);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return StorageProvisionerResult.Fail(
            (int)response.StatusCode,
            string.IsNullOrWhiteSpace(body)
                ? $"Artifact delete failed with status {(int)response.StatusCode}."
                : body
        );
    }

    public async Task<StorageProvisionerResult> ImportRepositoryBundleAsync(
        StorageNodeDto node,
        string apiToken,
        string physicalPath,
        byte[] bundlePlaintext,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(apiToken))
        {
            return StorageProvisionerResult.Fail(401, "Storage node API token is missing.");
        }

        var requestUri =
            $"http://{node.InternalHost}:{node.InternalHttpPort}/internal/repos/import-bundle";
        var payload = JsonSerializer.Serialize(
            new
            {
                physicalPath,
                bundleBase64 = Convert.ToBase64String(bundlePlaintext),
            }
        );
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

        using var response = await _httpClient
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if ((int)response.StatusCode is 200 or 201)
        {
            return StorageProvisionerResult.Ok((int)response.StatusCode);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return StorageProvisionerResult.Fail(
            (int)response.StatusCode,
            string.IsNullOrWhiteSpace(body)
                ? $"Bundle import failed with status {(int)response.StatusCode}."
                : body
        );
    }

    private async Task<StorageProvisionerResult> SendSyncFromAsync(
        StorageNodeDto node,
        string apiToken,
        string physicalPath,
        string sourceHost,
        string sourcePhysicalPath,
        int sourcePort,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(apiToken))
        {
            return StorageProvisionerResult.Fail(401, "Storage node API token is missing.");
        }

        var requestUri =
            $"http://{node.InternalHost}:{node.InternalHttpPort}/internal/repos/sync-from";
        var payload = JsonSerializer.Serialize(
            new
            {
                physicalPath,
                sourceHost,
                sourcePhysicalPath,
                sourcePort,
            }
        );
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

        using var response = await _httpClient
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if ((int)response.StatusCode == 200)
        {
            return StorageProvisionerResult.Ok((int)response.StatusCode);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var error = string.IsNullOrWhiteSpace(body)
            ? $"Storage sync-from failed with status {(int)response.StatusCode}."
            : body;

        return StorageProvisionerResult.Fail((int)response.StatusCode, error);
    }

    private async Task<StorageProvisionerResult> SendAsync(
        HttpMethod method,
        StorageNodeDto node,
        string apiToken,
        string physicalPath,
        long? receiveMaxBytes,
        string replicationRole,
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
        var payload = receiveMaxBytes is null
            ? JsonSerializer.Serialize(new { physicalPath, replicationRole })
            : JsonSerializer.Serialize(new { physicalPath, receiveMaxBytes, replicationRole });
        using var request = new HttpRequestMessage(method, requestUri)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
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
