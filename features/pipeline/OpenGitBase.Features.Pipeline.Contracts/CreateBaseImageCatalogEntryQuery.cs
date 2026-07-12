using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Pipeline.Contracts;

public sealed class CreateBaseImageCatalogEntryQuery
    : IQuery<BaseImageCatalogEntryDto, CreateBaseImageCatalogEntryQuery>
{
    public string Slug { get; set; } = string.Empty;

    public string VersionLabel { get; set; } = string.Empty;

    public string ArtifactUri { get; set; } = string.Empty;

    public string ContentHash { get; set; } = string.Empty;

    public string OciProvenance { get; set; } = string.Empty;

    public Guid CreatedByUserId { get; set; }
}
