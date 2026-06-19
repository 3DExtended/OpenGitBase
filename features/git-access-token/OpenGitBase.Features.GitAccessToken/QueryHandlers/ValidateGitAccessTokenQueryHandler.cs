using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.GitAccessToken.Contracts;
using OpenGitBase.Features.GitAccessToken.Entities;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.GitAccessToken.QueryHandlers;

public class ValidateGitAccessTokenQueryHandler
    : IQueryHandler<ValidateGitAccessTokenQuery, ValidateGitAccessTokenResult>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IPasswordHasherService _passwordHasherService;
    private readonly ISystemClock _systemClock;

    public ValidateGitAccessTokenQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IPasswordHasherService passwordHasherService,
        ISystemClock systemClock
    )
    {
        _contextFactory = contextFactory;
        _passwordHasherService = passwordHasherService;
        _systemClock = systemClock;
    }

    public async Task<Option<ValidateGitAccessTokenResult>> RunQueryAsync(
        ValidateGitAccessTokenQuery query,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(query.Token))
        {
            return Option<ValidateGitAccessTokenResult>.None;
        }

        var lookupHash = GitAccessTokenUtility.ComputeLookupHash(query.Token);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context
            .Set<GitAccessTokenEntity>()
            .FirstOrDefaultAsync(x => x.TokenLookupHash == lookupHash, cancellationToken);

        if (entity is null)
        {
            return Option<ValidateGitAccessTokenResult>.None;
        }

        if (
            entity.RevokedAt is not null
            || (entity.ExpiresAt is not null && entity.ExpiresAt <= _systemClock.UtcNow)
            || !_passwordHasherService.VerifyPassword(entity.TokenHash, query.Token)
        )
        {
            return Option<ValidateGitAccessTokenResult>.None;
        }

        return Option.From(
            new ValidateGitAccessTokenResult
            {
                UserId = UserId.From(entity.OwnerUserId),
                Scope = entity.Scope,
            }
        );
    }
}
