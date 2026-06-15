using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.Entities;

namespace OpenGitBase.Features.Organization.QueryHandlers;

public class RemoveOrganizationMemberQueryHandler
    : IQueryHandler<RemoveOrganizationMemberQuery, Unit>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public RemoveOrganizationMemberQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<Unit>> RunQueryAsync(
        RemoveOrganizationMemberQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var member = await context
            .Set<OrganizationMemberEntity>()
            .FirstOrDefaultAsync(
                x =>
                    x.OrganizationId == query.OrganizationId.Value
                    && x.UserId == query.UserId.Value,
                cancellationToken
            );

        if (member == null)
        {
            return Option<Unit>.None;
        }

        context.Remove(member);
        await context.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
