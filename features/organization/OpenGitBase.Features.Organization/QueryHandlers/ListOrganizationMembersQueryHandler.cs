using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.Entities;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Organization.QueryHandlers;

public class ListOrganizationMembersQueryHandler
    : IQueryHandler<ListOrganizationMembersQuery, IReadOnlyList<OrganizationMemberDto>>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public ListOrganizationMembersQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<IReadOnlyList<OrganizationMemberDto>>> RunQueryAsync(
        ListOrganizationMembersQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var members = await context
            .Set<OrganizationMemberEntity>()
            .AsNoTracking()
            .Where(x => x.OrganizationId == query.OrganizationId.Value)
            .Join(
                context.Set<UserEntity>().AsNoTracking(),
                member => member.UserId,
                user => user.Id,
                (member, user) =>
                    new OrganizationMemberDto
                    {
                        Id = OrganizationMemberId.From(member.Id),
                        OrganizationId = OrganizationId.From(member.OrganizationId),
                        UserId = UserId.From(member.UserId),
                        Username = user.Username,
                        Role = member.Role,
                    }
            )
            .ToListAsync(cancellationToken);

        return Option.From<IReadOnlyList<OrganizationMemberDto>>(members);
    }
}
