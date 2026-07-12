using System.Net.Http.Json;
using System.Security.Cryptography;
using OpenGitBase.Features.Pipeline.Contracts;

namespace OpenGitBase.ComputeAgent;

public sealed class PromotedDependencyLayerResolver : IPromotedDependencyLayerResolver
{
    private readonly LayerStoreFetchOptions _layerStoreOptions;

    public PromotedDependencyLayerResolver(LayerStoreFetchOptions layerStoreOptions)
    {
        _layerStoreOptions = layerStoreOptions;
    }

    public async Task<BaseImageArtifactFetchResult> FetchAsync(
        string recipeKey,
        HttpClient apiClient,
        CancellationToken cancellationToken
    )
    {
        var response = await apiClient
            .GetAsync(
                $"pipeline/dependency-layers/resolve?recipeKey={Uri.EscapeDataString(recipeKey)}",
                cancellationToken
            )
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return new BaseImageArtifactFetchResult { Success = false };
        }

        var artifact = await response.Content
            .ReadFromJsonAsync<BaseImageArtifactDto>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (artifact is null || string.IsNullOrWhiteSpace(artifact.LayerStoreObjectKey))
        {
            return new BaseImageArtifactFetchResult { Success = false };
        }

        var cacheDir = Path.Combine(Path.GetTempPath(), "opengitbase-agent", "dependency-layers");
        Directory.CreateDirectory(cacheDir);
        var localPath = Path.Combine(cacheDir, artifact.ContentHash);
        if (File.Exists(localPath))
        {
            return new BaseImageArtifactFetchResult
            {
                Success = true,
                LocalPath = localPath,
                Artifact = artifact,
            };
        }

        var layerStoreUrl =
            $"{_layerStoreOptions.Endpoint.TrimEnd('/')}/{_layerStoreOptions.Bucket}/{artifact.LayerStoreObjectKey}";
        await using var blobStream = await apiClient
            .GetStreamAsync(layerStoreUrl, cancellationToken)
            .ConfigureAwait(false);
        await using var fileStream = File.Create(localPath);
        await blobStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);

        var actualHash = await ComputeSha256HexAsync(localPath, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(actualHash, artifact.ContentHash, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(localPath);
            return new BaseImageArtifactFetchResult
            {
                Success = false,
                ErrorMessage = $"Hash mismatch for promoted layer '{recipeKey}'.",
            };
        }

        return new BaseImageArtifactFetchResult
        {
            Success = true,
            LocalPath = localPath,
            Artifact = artifact,
        };
    }

    private static async Task<string> ComputeSha256HexAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
