using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.Entities;

namespace OpenGitBase.Features.Organization.QueryHandlers;

public class DeclineOrganizationInviteQueryHandler
    : IQueryHandler<DeclineOrganizationInviteQuery, Unit>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IPasswordHasherService _passwordHasherService;
    private readonly ISystemClock _systemClock;

    public DeclineOrganizationInviteQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IPasswordHasherService passwordHasherService,
        ISystemClock systemClock
    )
    {
        _contextFactory = contextFactory;
        _passwordHasherService = passwordHasherService;
        _systemClock = systemClock;
    }

    public async Task<Option<Unit>> RunQueryAsync(
        DeclineOrganizationInviteQuery query,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(query.Token))
        {
            return Option<Unit>.None;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var invites = await context
            .Set<OrganizationInviteEntity>()
            .Where(x => x.Status == OrganizationInviteStatus.Pending)
            .ToListAsync(cancellationToken);
        var invite = OrganizationInviteTokenUtility.FindByToken(
            invites,
            _passwordHasherService,
            query.Token
        );
        if (invite == null || invite.ExpiresAt <= _systemClock.UtcNow)
        {
            return Option<Unit>.None;
        }

        invite.Status = OrganizationInviteStatus.Declined;
        await context.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
