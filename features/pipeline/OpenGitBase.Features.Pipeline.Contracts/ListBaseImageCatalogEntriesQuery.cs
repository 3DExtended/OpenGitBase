using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Pipeline.Contracts;

public sealed class ListBaseImageCatalogEntriesQuery
    : IQuery<IReadOnlyList<BaseImageCatalogEntryDto>, ListBaseImageCatalogEntriesQuery>
{
}
