using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.Pipeline.Contracts;

public sealed record BaseImageCatalogEntryId : Identifier<Guid, BaseImageCatalogEntryId>;
