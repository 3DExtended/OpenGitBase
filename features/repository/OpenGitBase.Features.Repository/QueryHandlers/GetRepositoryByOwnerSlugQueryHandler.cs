using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Entities;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Repository.QueryHandlers;

public class GetRepositoryByOwnerSlugQueryHandler
    : IQueryHandler<GetRepositoryByOwnerSlugQuery, RepositoryDto>
{
    private readonly IMapper _mapper;
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public GetRepositoryByOwnerSlugQueryHandler(
        IMapper mapper,
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _mapper = mapper;
        _contextFactory = contextFactory;
    }

    public async Task<Option<RepositoryDto>> RunQueryAsync(
        GetRepositoryByOwnerSlugQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var ownerSlug = query.OwnerSlug.Trim();
        var normalizedOwnerSlug = ownerSlug.ToLowerInvariant();

        var user = await context
            .Set<UserEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.NormalizedUsername.ToLower() == normalizedOwnerSlug,
                cancellationToken);

        RepositoryEntity? entity = null;

        if (user != null)
        {
            entity = await context
                .Set<RepositoryEntity>()
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.OwnerUserId == user.Id && x.Slug == query.Slug,
                    cancellationToken
                );
        }
        else
        {
            var org = await context
                .Set<OrganizationEntity>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Slug.ToLower() == normalizedOwnerSlug, cancellationToken);

            if (org != null)
            {
                entity = await context
                    .Set<RepositoryEntity>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x => x.OwnerUserId == org.Id && x.Slug == query.Slug,
                        cancellationToken
                    );
            }
        }

        if (entity == null)
        {
            return Option.None;
        }

        var dto = _mapper.Map<RepositoryDto>(entity);
        await RepositoryOwnerMetadataEnricher
            .EnrichAsync([dto], context, cancellationToken)
            .ConfigureAwait(false);
        return Option.From(dto);
    }
}
