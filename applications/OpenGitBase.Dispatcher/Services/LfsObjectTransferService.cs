using OpenGitBase.Dispatcher.Models;

namespace OpenGitBase.Dispatcher.Services;

/// <summary>
/// Streams LFS objects to and from storage nodes on their git HTTP port. Its HttpClient has no overall timeout:
/// a large object may take long, and an aborted client request cancels the transfer.
/// </summary>
public sealed class LfsObjectTransferService
{
    private readonly HttpClient _httpClient;

    public LfsObjectTransferService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<HttpResponseMessage> UploadAsync(
        StorageRoutingTarget target,
        Guid repositoryId,
        string oid,
        Stream body,
        long size,
        CancellationToken cancellationToken
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, ObjectUri(target, repositoryId, oid))
        {
            Content = new StreamContent(body),
        };
        request.Content.Headers.ContentLength = size;
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        return await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public Task<HttpResponseMessage> OpenDownloadAsync(
        StorageRoutingTarget target,
        Guid repositoryId,
        string oid,
        CancellationToken cancellationToken
    ) =>
        _httpClient.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, ObjectUri(target, repositoryId, oid)),
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );

    private static string ObjectUri(StorageRoutingTarget target, Guid repositoryId, string oid) =>
        $"http://{target.InternalHost}:{target.InternalGitHttpPort}/lfs/{repositoryId:D}/objects/{oid}";
}
