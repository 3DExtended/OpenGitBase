using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.Entities;

namespace OpenGitBase.Features.Organization.QueryHandlers;

public class ListOrganizationInvitesQueryHandler
    : IQueryHandler<ListOrganizationInvitesQuery, IReadOnlyList<OrganizationInviteDto>>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IEmailProtectionService _emailProtectionService;
    private readonly ISystemClock _systemClock;

    public ListOrganizationInvitesQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IEmailProtectionService emailProtectionService,
        ISystemClock systemClock
    )
    {
        _contextFactory = contextFactory;
        _emailProtectionService = emailProtectionService;
        _systemClock = systemClock;
    }

    public async Task<Option<IReadOnlyList<OrganizationInviteDto>>> RunQueryAsync(
        ListOrganizationInvitesQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var utcNow = _systemClock.UtcNow;
        var invites = await context
            .Set<OrganizationInviteEntity>()
            .AsNoTracking()
            .Where(x => x.OrganizationId == query.OrganizationId.Value)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        invites = invites
            .Where(x => x.Status == OrganizationInviteStatus.Pending && x.ExpiresAt > utcNow)
            .ToList();

        var mapped = invites
            .Select(invite =>
            {
                var email = _emailProtectionService.DecryptEmail(invite.EmailCiphertext);
                return new OrganizationInviteDto
                {
                    Id = OrganizationInviteId.From(invite.Id),
                    OrganizationId = OrganizationId.From(invite.OrganizationId),
                    Email = query.RevealEmail
                        ? email
                        : OrganizationInviteTokenUtility.RedactEmail(email),
                    Role = invite.Role,
                    InvitedByUserId = invite.InvitedByUserId,
                    CreatedAt = invite.CreatedAt,
                    ExpiresAt = invite.ExpiresAt,
                    Status = OrganizationInviteTokenUtility.ResolveStatus(invite, utcNow),
                };
            })
            .ToList();

        return Option.From<IReadOnlyList<OrganizationInviteDto>>(mapped);
    }
}
