using OpenGitBase.Common.Options;

namespace OpenGitBase.Api.Services;

public sealed class LayerStoreClient : ILayerStoreClient
{
    private readonly HttpClient _httpClient;
    private readonly LayerStoreOptions _options;

    public LayerStoreClient(HttpClient httpClient, LayerStoreOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task PutBlobAsync(string hash, Stream stream, CancellationToken cancellationToken)
    {
        var path = BuildPath(hash);
        using var request = new HttpRequestMessage(HttpMethod.Put, path)
        {
            Content = new StreamContent(stream),
        };
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Stream> GetBlobAsync(string hash, CancellationToken cancellationToken)
    {
        var path = BuildPath(hash);
        var response = await _httpClient.GetAsync(path, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
    }

    private string BuildPath(string hash) => $"{_options.Endpoint.TrimEnd('/')}/{_options.Bucket}/{hash}";
}
