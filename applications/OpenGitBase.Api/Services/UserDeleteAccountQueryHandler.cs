using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Entities;
using OpenGitBase.Features.PublicGitSshKey.Entities;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.RepositoryMember.Entities;
using OpenGitBase.Features.Users.Contracts.Queries.Users;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Api.Services;

public class UserDeleteAccountQueryHandler
    : IQueryHandler<UserDeleteAccountQuery, UserDeleteAccountResult>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IPasswordHasherService _passwordHasherService;

    public UserDeleteAccountQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IPasswordHasherService passwordHasherService
    )
    {
        _contextFactory = contextFactory;
        _passwordHasherService = passwordHasherService;
    }

    public async Task<Option<UserDeleteAccountResult>> RunQueryAsync(
        UserDeleteAccountQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var credentials = await context
            .Set<UserCredentialsEntity>()
            .Include(x => x.User)
            .FirstOrDefaultAsync(
                x => x.UserId == query.UserId.Value && !x.Deleted,
                cancellationToken
            );

        if (
            credentials == null
            || credentials.SignInProvider
            || string.IsNullOrEmpty(credentials.PasswordHash)
            || !_passwordHasherService.VerifyPassword(credentials.PasswordHash, query.Password)
        )
        {
            return Option<UserDeleteAccountResult>.None;
        }

        var blockers = new List<UserDeleteAccountBlocker>();
        var ownedRepos = await context
            .Set<RepositoryEntity>()
            .Where(x => x.OwnerUserId == query.UserId.Value)
            .ToListAsync(cancellationToken);

        foreach (var repo in ownedRepos)
        {
            blockers.Add(
                new UserDeleteAccountBlocker
                {
                    Type = "repository",
                    Name = repo.Name,
                    Slug = repo.Slug,
                }
            );
        }

        var ownedOrgs = await context
            .Set<OrganizationEntity>()
            .Where(x => x.OwnerUserId == query.UserId.Value)
            .ToListAsync(cancellationToken);

        foreach (var org in ownedOrgs)
        {
            blockers.Add(
                new UserDeleteAccountBlocker
                {
                    Type = "organization",
                    Name = org.Name,
                    Slug = org.Slug,
                }
            );
        }

        if (blockers.Count > 0)
        {
            return Option.From(
                new UserDeleteAccountResult { Success = false, Blockers = blockers }
            );
        }

        var sshKeys = await context
            .Set<PublicGitSshKeyEntity>()
            .Where(x => x.OwnerUserId == query.UserId.Value)
            .ToListAsync(cancellationToken);
        context.RemoveRange(sshKeys);

        var memberships = await context
            .Set<RepositoryMemberEntity>()
            .Where(x => x.UserId == query.UserId.Value)
            .ToListAsync(cancellationToken);
        context.RemoveRange(memberships);

        credentials.Deleted = true;
        credentials.PasswordHash = null;
        context.Remove(credentials.User);
        await context.SaveChangesAsync(cancellationToken);

        return Option.From(new UserDeleteAccountResult { Success = true });
    }
}
