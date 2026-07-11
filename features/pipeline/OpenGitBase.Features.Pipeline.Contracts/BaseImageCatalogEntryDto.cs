namespace OpenGitBase.Features.Pipeline.Contracts;

public sealed class BaseImageCatalogEntryDto
{
    public BaseImageCatalogEntryId Id { get; set; } = BaseImageCatalogEntryId.From(Guid.NewGuid());

    public string Slug { get; set; } = string.Empty;

    public string VersionLabel { get; set; } = string.Empty;

    public string ArtifactUri { get; set; } = string.Empty;

    public string OciProvenance { get; set; } = string.Empty;

    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
