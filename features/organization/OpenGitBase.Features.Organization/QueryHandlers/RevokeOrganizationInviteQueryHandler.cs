using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.Entities;

namespace OpenGitBase.Features.Organization.QueryHandlers;

public class RevokeOrganizationInviteQueryHandler
    : IQueryHandler<RevokeOrganizationInviteQuery, Unit>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public RevokeOrganizationInviteQueryHandler(IDbContextFactory<OpenGitBaseDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<Unit>> RunQueryAsync(
        RevokeOrganizationInviteQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var invite = await context
            .Set<OrganizationInviteEntity>()
            .FirstOrDefaultAsync(
                x =>
                    x.OrganizationId == query.OrganizationId.Value
                    && x.Id == query.InviteId.Value
                    && x.Status == OrganizationInviteStatus.Pending,
                cancellationToken
            );
        if (invite == null)
        {
            return Option<Unit>.None;
        }

        invite.Status = OrganizationInviteStatus.Revoked;
        await context.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
