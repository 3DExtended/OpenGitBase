namespace OpenGitBase.Api.Services;

public interface ILayerStoreClient
{
    Task PutBlobAsync(string hash, Stream stream, CancellationToken cancellationToken);

    Task<Stream> GetBlobAsync(string hash, CancellationToken cancellationToken);
}
