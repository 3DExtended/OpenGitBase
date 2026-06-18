using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.Entities;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Organization.QueryHandlers;

public class AcceptOrganizationInviteQueryHandler
    : IQueryHandler<AcceptOrganizationInviteQuery, AcceptOrganizationInviteResult>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IPasswordHasherService _passwordHasherService;
    private readonly ISystemClock _systemClock;

    public AcceptOrganizationInviteQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IPasswordHasherService passwordHasherService,
        ISystemClock systemClock
    )
    {
        _contextFactory = contextFactory;
        _passwordHasherService = passwordHasherService;
        _systemClock = systemClock;
    }

    public async Task<Option<AcceptOrganizationInviteResult>> RunQueryAsync(
        AcceptOrganizationInviteQuery query,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(query.Token))
        {
            return Option.From(AcceptOrganizationInviteResult.NotFound);
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
        if (invite == null)
        {
            return Option.From(AcceptOrganizationInviteResult.NotFound);
        }

        var utcNow = _systemClock.UtcNow;
        if (invite.ExpiresAt <= utcNow)
        {
            return Option.From(AcceptOrganizationInviteResult.Expired);
        }

        var credentials = await context
            .Set<UserCredentialsEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.UserId == query.UserId.Value && !x.Deleted && !string.IsNullOrEmpty(x.EmailLookupHash),
                cancellationToken
            );
        if (credentials == null || credentials.EmailLookupHash != invite.EmailLookupHash)
        {
            return Option.From(AcceptOrganizationInviteResult.EmailMismatch);
        }

        var existingMember = await context
            .Set<OrganizationMemberEntity>()
            .AnyAsync(
                x => x.OrganizationId == invite.OrganizationId && x.UserId == query.UserId.Value,
                cancellationToken
            );
        if (existingMember)
        {
            return Option.From(AcceptOrganizationInviteResult.AlreadyMember);
        }

        context.Set<OrganizationMemberEntity>().Add(
            new OrganizationMemberEntity
            {
                Id = Guid.NewGuid(),
                OrganizationId = invite.OrganizationId,
                UserId = query.UserId.Value,
                Role = invite.Role,
            }
        );
        invite.Status = OrganizationInviteStatus.Accepted;
        await context.SaveChangesAsync(cancellationToken);
        return Option.From(AcceptOrganizationInviteResult.Accepted);
    }
}
