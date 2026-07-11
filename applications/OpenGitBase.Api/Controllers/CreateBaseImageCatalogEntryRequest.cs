namespace OpenGitBase.Api.Controllers;

public sealed class CreateBaseImageCatalogEntryRequest
{
    public string Slug { get; set; } = string.Empty;

    public string VersionLabel { get; set; } = string.Empty;

    public string ArtifactUri { get; set; } = string.Empty;

    public string OciProvenance { get; set; } = string.Empty;

    public Guid CreatedByUserId { get; set; }
}
