using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Entities;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Repository.QueryHandlers;

public class GetOwnerProfileQueryHandler : IQueryHandler<GetOwnerProfileQuery, OwnerProfileDto>
{
    private readonly IMapper _mapper;
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public GetOwnerProfileQueryHandler(
        IMapper mapper,
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _mapper = mapper;
        _contextFactory = contextFactory;
    }

    public async Task<Option<OwnerProfileDto>> RunQueryAsync(
        GetOwnerProfileQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var ownerSlug = query.OwnerSlug.Trim().ToLowerInvariant();

        var user = await context
            .Set<UserEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.NormalizedUsername == ownerSlug, cancellationToken);

        if (user != null)
        {
            var repos = await context
                .Set<RepositoryEntity>()
                .AsNoTracking()
                .Where(x => x.OwnerUserId == user.Id && !x.IsPrivate)
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken);

            return Option.From(
                new OwnerProfileDto
                {
                    Slug = user.Username,
                    Name = user.Username,
                    Kind = "user",
                    Repositories = repos.Select(x => _mapper.Map<RepositoryDto>(x)).ToList(),
                }
            );
        }

        var org = await context
            .Set<OrganizationEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Slug.ToLower() == ownerSlug, cancellationToken);

        if (org == null)
        {
            return Option<OwnerProfileDto>.None;
        }

        var orgRepos = await context
            .Set<RepositoryEntity>()
            .AsNoTracking()
            .Where(x => x.OwnerUserId == org.Id && !x.IsPrivate)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return Option.From(
            new OwnerProfileDto
            {
                Slug = org.Slug,
                Name = org.Name,
                Kind = "organization",
                Repositories = orgRepos.Select(x => _mapper.Map<RepositoryDto>(x)).ToList(),
            }
        );
    }
}
