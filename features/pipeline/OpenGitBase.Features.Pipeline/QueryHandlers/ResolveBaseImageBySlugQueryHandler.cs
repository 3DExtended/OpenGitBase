using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;

namespace OpenGitBase.Features.Pipeline.QueryHandlers;

public sealed class ResolveBaseImageBySlugQueryHandler
    : IQueryHandler<ResolveBaseImageBySlugQuery, BaseImageArtifactDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public ResolveBaseImageBySlugQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<BaseImageArtifactDto>> RunQueryAsync(
        ResolveBaseImageBySlugQuery query,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(query.Slug))
        {
            return Option<BaseImageArtifactDto>.None;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entry = await context
            .Set<BaseImageCatalogEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Slug == query.Slug.Trim(), cancellationToken)
            .ConfigureAwait(false);
        if (entry is null || string.IsNullOrWhiteSpace(entry.ArtifactUri))
        {
            return Option<BaseImageArtifactDto>.None;
        }

        var contentHash = string.IsNullOrWhiteSpace(entry.ContentHash)
            ? entry.ArtifactUri
            : entry.ContentHash;

        return Option.From(
            new BaseImageArtifactDto
            {
                Slug = entry.Slug,
                ContentHash = contentHash,
                LayerStoreObjectKey = entry.ArtifactUri,
                OciProvenance = entry.OciProvenance,
            }
        );
    }
}
