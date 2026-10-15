using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using OpenGitBase.Features.Repository.Contracts;

namespace OpenGitBase.Api.Services;

public sealed class StorageLfsClient : IStorageLfsClient
{
    // The HttpClient has no timeout (transfers bound themselves); deleting a large LFS directory may take a while,
    // and the node finishes it even when the caller stops waiting.
    private static readonly TimeSpan DeleteTimeout = TimeSpan.FromMinutes(2);

    private readonly HttpClient _httpClient;

    public StorageLfsClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<long?> GetObjectSizeAsync(
        StorageNodeEndpoint node,
        Guid repositoryId,
        string oid,
        LfsObjectCopyKind kind,
        CancellationToken cancellationToken
    )
    {
        using var request = Request(HttpMethod.Head, node, repositoryId, oid, kind);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return response.Content.Headers.ContentLength
            ?? throw new HttpRequestException($"Storage node {node.InternalHost} sent no object size.");
    }

    public async Task<Stream?> OpenObjectAsync(
        StorageNodeEndpoint node,
        Guid repositoryId,
        string oid,
        LfsObjectCopyKind kind,
        CancellationToken cancellationToken
    )
    {
        using var request = Request(HttpMethod.Get, node, repositoryId, oid, kind);
        var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            response.Dispose();
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            response.Dispose();
            throw new HttpRequestException(
                $"Storage node {node.InternalHost} answered {(int)response.StatusCode} for LFS object {oid}.",
                null,
                response.StatusCode
            );
        }

        // Disposing the content stream releases the connection.
        return await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task PutObjectAsync(
        StorageNodeEndpoint node,
        Guid repositoryId,
        string oid,
        LfsObjectCopyKind kind,
        HttpContent content,
        CancellationToken cancellationToken
    )
    {
        using var request = Request(HttpMethod.Put, node, repositoryId, oid, kind);
        request.Content = content;
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Storage node {node.InternalHost} refused LFS object {oid} with {(int)response.StatusCode}.",
                null,
                response.StatusCode
            );
        }
    }

    public async Task<LfsNodeInventory> GetInventoryAsync(
        StorageNodeEndpoint node,
        Guid repositoryId,
        CancellationToken cancellationToken
    )
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"http://{node.InternalHost}:{node.InternalHttpPort}/internal/lfs/{repositoryId:D}/inventory"
        );
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", node.ApiToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Storage node {node.InternalHost} answered {(int)response.StatusCode} for the LFS inventory.",
                null,
                response.StatusCode
            );
        }

        InventoryBody? body;
        try
        {
            body = await response
                .Content.ReadFromJsonAsync<InventoryBody>(JsonSerializerOptions.Web, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new HttpRequestException($"Storage node {node.InternalHost} sent an unreadable LFS inventory.", ex);
        }

        var kind = body?.Kind switch
        {
            "plaintext" => LfsObjectCopyKind.Plaintext,
            "encrypted" => LfsObjectCopyKind.Encrypted,
            _ => throw new HttpRequestException($"Storage node {node.InternalHost} sent an LFS inventory of unknown kind."),
        };
        var objects = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var item in body.Objects ?? [])
        {
            objects[item.Oid] = item.Size;
        }

        return new LfsNodeInventory(kind, objects);
    }

    public async Task<bool> DeleteRepositoryAsync(
        StorageNodeEndpoint node,
        Guid repositoryId,
        CancellationToken cancellationToken
    )
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"http://{node.InternalHost}:{node.InternalHttpPort}/internal/lfs/{repositoryId:D}"
        );
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", node.ApiToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(DeleteTimeout);
        using var response = await _httpClient.SendAsync(request, timeout.Token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Storage node {node.InternalHost} answered {(int)response.StatusCode} when deleting LFS data.",
                null,
                response.StatusCode
            );
        }

        var body = await response
            .Content.ReadFromJsonAsync<DeleteBody>(JsonSerializerOptions.Web, timeout.Token)
            .ConfigureAwait(false);
        return body?.Deleted ?? false;
    }

    private static HttpRequestMessage Request(
        HttpMethod method,
        StorageNodeEndpoint node,
        Guid repositoryId,
        string oid,
        LfsObjectCopyKind kind
    )
    {
        var route = kind == LfsObjectCopyKind.Encrypted ? "encrypted" : "objects";
        var request = new HttpRequestMessage(
            method,
            $"http://{node.InternalHost}:{node.InternalHttpPort}/internal/lfs/{repositoryId:D}/{route}/{oid}"
        );
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", node.ApiToken);
        return request;
    }

    private sealed record InventoryBody(string? Kind, IReadOnlyList<InventoryItem>? Objects);

    private sealed record InventoryItem(string Oid, long Size);

    private sealed record DeleteBody(bool Deleted);
}
