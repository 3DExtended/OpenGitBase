namespace OpenGitBase.ComputeAgent;

public interface IBaseImageArtifactResolver
{
    Task<BaseImageArtifactFetchResult> FetchAsync(
        string slug,
        HttpClient apiClient,
        CancellationToken cancellationToken
    );
}
