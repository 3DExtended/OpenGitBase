using System.Net;
using System.Net.Http.Headers;

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
        CancellationToken cancellationToken
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, ObjectUri(node, repositoryId, oid));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", node.ApiToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return response.Content.Headers.ContentLength
            ?? throw new HttpRequestException($"Storage node {node.InternalHost} sent no object size.");
    }

    private static string ObjectUri(StorageNodeEndpoint node, Guid repositoryId, string oid) =>
        $"http://{node.InternalHost}:{node.InternalHttpPort}/internal/lfs/{repositoryId:D}/objects/{oid}";
}
