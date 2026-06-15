using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.Entities;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Organization.QueryHandlers;

public class GetOrganizationMemberQueryHandler
    : IQueryHandler<GetOrganizationMemberQuery, OrganizationMemberDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public GetOrganizationMemberQueryHandler(IDbContextFactory<OpenGitBaseDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<OrganizationMemberDto>> RunQueryAsync(
        GetOrganizationMemberQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var member = await context
            .Set<OrganizationMemberEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x =>
                    x.OrganizationId == query.OrganizationId.Value
                    && x.UserId == query.UserId.Value,
                cancellationToken
            );

        if (member == null)
        {
            return Option<OrganizationMemberDto>.None;
        }

        return Option.From(
            new OrganizationMemberDto
            {
                Id = OrganizationMemberId.From(member.Id),
                OrganizationId = OrganizationId.From(member.OrganizationId),
                UserId = UserId.From(member.UserId),
                Role = member.Role,
            }
        );
    }
}
