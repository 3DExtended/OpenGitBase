using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.SendGrid;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.Entities;

namespace OpenGitBase.Features.Organization.QueryHandlers;

public class ResendOrganizationInviteQueryHandler
    : IQueryHandler<ResendOrganizationInviteQuery, Unit>
{
    private static readonly TimeSpan InviteLifetime = TimeSpan.FromDays(7);
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IEmailProtectionService _emailProtectionService;
    private readonly IPasswordHasherService _passwordHasherService;
    private readonly IQueryProcessor _queryProcessor;
    private readonly ISystemClock _systemClock;

    public ResendOrganizationInviteQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IEmailProtectionService emailProtectionService,
        IPasswordHasherService passwordHasherService,
        IQueryProcessor queryProcessor,
        ISystemClock systemClock
    )
    {
        _contextFactory = contextFactory;
        _emailProtectionService = emailProtectionService;
        _passwordHasherService = passwordHasherService;
        _queryProcessor = queryProcessor;
        _systemClock = systemClock;
    }

    public async Task<Option<Unit>> RunQueryAsync(
        ResendOrganizationInviteQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var inviteWithOrganization = await context
            .Set<OrganizationInviteEntity>()
            .Where(x =>
                x.OrganizationId == query.OrganizationId.Value
                && x.Id == query.InviteId.Value
                && x.Status == OrganizationInviteStatus.Pending
            )
            .Join(
                context.Set<OrganizationEntity>(),
                invite => invite.OrganizationId,
                organization => organization.Id,
                (invite, organization) => new { Invite = invite, Organization = organization }
            )
            .FirstOrDefaultAsync(cancellationToken);
        if (inviteWithOrganization == null)
        {
            return Option<Unit>.None;
        }

        var token = OrganizationInviteTokenUtility.GenerateToken();
        inviteWithOrganization.Invite.TokenHash = _passwordHasherService.HashPassword(token);
        inviteWithOrganization.Invite.ExpiresAt = _systemClock.UtcNow.Add(InviteLifetime);
        await context.SaveChangesAsync(cancellationToken);

        var email = _emailProtectionService.DecryptEmail(inviteWithOrganization.Invite.EmailCiphertext);
        var emailResult = await _queryProcessor
            .RunQueryAsync(
                new EmailSendQuery
                {
                    To = new EmailAddress { Email = email },
                    Subject = $"Invitation to join {inviteWithOrganization.Organization.Name}",
                    HtmlBody =
                        $"You have been invited to join {inviteWithOrganization.Organization.Name}.<br><br>Accept invitation: <a href=\"/invite/{token}\">/invite/{token}</a>",
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return emailResult.IsSome ? Unit.Value : Option<Unit>.None;
    }
}
