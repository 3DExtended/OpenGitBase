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
        var ownerSlug = query.OwnerSlug.Trim().ToLowerInvariant();

        var user = await context
            .Set<UserEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.NormalizedUsername == ownerSlug, cancellationToken);

        RepositoryEntity? entity = null;

        if (user != null)
        {
            entity = await context
                .Set<RepositoryEntity>()
                .AsNoTracking()
                .Include(x => x.OwnerUser)
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
                .FirstOrDefaultAsync(x => x.Slug.ToLower() == ownerSlug, cancellationToken);

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

        return entity == null ? Option.None : Option.From(_mapper.Map<RepositoryDto>(entity));
    }
}
