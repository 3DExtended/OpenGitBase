using System.Net;
using System.Net.Http.Headers;
using OpenGitBase.Features.Repository.Contracts;

namespace OpenGitBase.Api.Services;

public sealed class StorageLfsClient : IStorageLfsClient
{
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
}
