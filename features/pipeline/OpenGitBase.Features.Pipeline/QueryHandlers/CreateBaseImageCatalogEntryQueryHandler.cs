using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;

namespace OpenGitBase.Features.Pipeline.QueryHandlers;

public sealed class CreateBaseImageCatalogEntryQueryHandler
    : IQueryHandler<CreateBaseImageCatalogEntryQuery, BaseImageCatalogEntryDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IMapper _mapper;

    public CreateBaseImageCatalogEntryQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IMapper mapper
    )
    {
        _contextFactory = contextFactory;
        _mapper = mapper;
    }

    public async Task<Option<BaseImageCatalogEntryDto>> RunQueryAsync(
        CreateBaseImageCatalogEntryQuery query,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(query.Slug) || string.IsNullOrWhiteSpace(query.ArtifactUri))
        {
            return Option<BaseImageCatalogEntryDto>.None;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await context
            .Set<BaseImageCatalogEntity>()
            .FirstOrDefaultAsync(entity => entity.Slug == query.Slug, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return Option<BaseImageCatalogEntryDto>.None;
        }

        var entity = new BaseImageCatalogEntity
        {
            Id = Guid.NewGuid(),
            Slug = query.Slug,
            VersionLabel = query.VersionLabel,
            ArtifactUri = query.ArtifactUri,
            ContentHash = string.IsNullOrWhiteSpace(query.ContentHash)
                ? query.ArtifactUri
                : query.ContentHash,
            OciProvenance = query.OciProvenance,
            CreatedByUserId = query.CreatedByUserId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        context.Set<BaseImageCatalogEntity>().Add(entity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Option.From(_mapper.Map<BaseImageCatalogEntryDto>(entity));
    }
}
