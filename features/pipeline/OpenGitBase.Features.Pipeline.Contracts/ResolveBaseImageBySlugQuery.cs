using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Pipeline.Contracts;

public sealed class BaseImageArtifactDto
{
    public string Slug { get; set; } = string.Empty;

    public string ContentHash { get; set; } = string.Empty;

    public string LayerStoreObjectKey { get; set; } = string.Empty;

    public string OciProvenance { get; set; } = string.Empty;
}

public sealed class ResolveBaseImageBySlugQuery
    : IQuery<BaseImageArtifactDto, ResolveBaseImageBySlugQuery>
{
    public string Slug { get; set; } = string.Empty;
}
