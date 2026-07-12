namespace OpenGitBase.ComputeAgent;

public interface IPromotedDependencyLayerResolver
{
    Task<BaseImageArtifactFetchResult> FetchAsync(
        string recipeKey,
        HttpClient apiClient,
        CancellationToken cancellationToken
    );
}
