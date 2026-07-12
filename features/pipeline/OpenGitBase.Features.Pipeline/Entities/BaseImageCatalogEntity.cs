using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.Pipeline.Entities;

public sealed class BaseImageCatalogEntity : IIdentifiableEntity<Guid>
{
    public Guid Id { get; set; }

    public string Slug { get; set; } = string.Empty;

    public string VersionLabel { get; set; } = string.Empty;

    public string ArtifactUri { get; set; } = string.Empty;

    public string ContentHash { get; set; } = string.Empty;

    public string OciProvenance { get; set; } = string.Empty;

    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
