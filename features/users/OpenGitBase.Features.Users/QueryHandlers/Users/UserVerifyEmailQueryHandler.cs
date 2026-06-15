using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.SendGrid;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Contracts.Queries.Users;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Users.QueryHandlers.Users;

public class UserVerifyEmailQueryHandler : IQueryHandler<UserVerifyEmailQuery, Unit>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IPasswordHasherService _passwordHasherService;
    private readonly ISystemClock _systemClock;

    public UserVerifyEmailQueryHandler(
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
        UserVerifyEmailQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var credentials = await context
            .Set<UserCredentialsEntity>()
            .FirstOrDefaultAsync(
                x => x.Username == query.Username && !x.Deleted,
                cancellationToken
            );

        if (
            credentials == null
            || credentials.EmailVerified
            || string.IsNullOrEmpty(credentials.EmailVerificationTokenHash)
            || credentials.EmailVerificationTokenExpireDate == null
        )
        {
            return Option<Unit>.None;
        }

        if (credentials.EmailVerificationTokenExpireDate < _systemClock.UtcNow)
        {
            return Option<Unit>.None;
        }

        if (
            !_passwordHasherService.VerifyPassword(
                credentials.EmailVerificationTokenHash,
                query.VerificationToken
            )
        )
        {
            return Option<Unit>.None;
        }

        credentials.EmailVerified = true;
        credentials.EmailVerificationTokenHash = null;
        credentials.EmailVerificationTokenExpireDate = null;
        await context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
