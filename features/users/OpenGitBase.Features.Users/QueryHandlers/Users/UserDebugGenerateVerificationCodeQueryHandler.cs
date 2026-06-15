using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Contracts.Queries.Users;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Users.QueryHandlers.Users;

public class UserDebugGenerateVerificationCodeQueryHandler
    : IQueryHandler<UserDebugGenerateVerificationCodeQuery, UserDebugVerificationCode>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IPasswordHasherService _passwordHasherService;
    private readonly ISystemClock _systemClock;

    public UserDebugGenerateVerificationCodeQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IPasswordHasherService passwordHasherService,
        ISystemClock systemClock
    )
    {
        _contextFactory = contextFactory;
        _passwordHasherService = passwordHasherService;
        _systemClock = systemClock;
    }

    public async Task<Option<UserDebugVerificationCode>> RunQueryAsync(
        UserDebugGenerateVerificationCodeQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var credentials = await context
            .Set<UserCredentialsEntity>()
            .FirstOrDefaultAsync(
                x => x.UserId == query.UserId.Value && !x.Deleted,
                cancellationToken
            );

        if (
            credentials == null
            || credentials.SignInProvider
            || credentials.EmailVerified
            || string.IsNullOrEmpty(credentials.EmailCiphertext)
        )
        {
            return Option<UserDebugVerificationCode>.None;
        }

        var verificationCode =
            $"{RandomNumberGenerator.GetInt32(100, 999)}-{RandomNumberGenerator.GetInt32(100, 999)}-{RandomNumberGenerator.GetInt32(100, 999)}";
        var expiresAt = _systemClock.UtcNow.AddHours(24);
        credentials.EmailVerificationTokenHash = _passwordHasherService.HashPassword(
            verificationCode
        );
        credentials.EmailVerificationTokenExpireDate = expiresAt;
        await context.SaveChangesAsync(cancellationToken);

        return Option.From(
            new UserDebugVerificationCode { Code = verificationCode, ExpiresAt = expiresAt }
        );
    }
}
