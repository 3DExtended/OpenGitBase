using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.Entities;

namespace OpenGitBase.Features.Organization.QueryHandlers;

public class GetOrganizationBySlugQueryHandler
    : IQueryHandler<GetOrganizationBySlugQuery, OrganizationDto>
{
    private readonly IMapper _mapper;
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public GetOrganizationBySlugQueryHandler(
        IMapper mapper,
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _mapper = mapper;
        _contextFactory = contextFactory;
    }

    public async Task<Option<OrganizationDto>> RunQueryAsync(
        GetOrganizationBySlugQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var slug = query.Slug.Trim().ToLowerInvariant();
        var entity = await context
            .Set<OrganizationEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Slug.ToLower() == slug, cancellationToken);

        return entity == null
            ? Option<OrganizationDto>.None
            : Option.From(_mapper.Map<OrganizationDto>(entity));
    }
}
