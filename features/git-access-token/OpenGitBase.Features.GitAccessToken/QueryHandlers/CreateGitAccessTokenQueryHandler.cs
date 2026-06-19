using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.GitAccessToken.Contracts;
using OpenGitBase.Features.GitAccessToken.Entities;

namespace OpenGitBase.Features.GitAccessToken.QueryHandlers;

public class CreateGitAccessTokenQueryHandler
    : IQueryHandler<CreateGitAccessTokenQuery, CreateGitAccessTokenResult>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IPasswordHasherService _passwordHasherService;
    private readonly ISystemClock _systemClock;
    private readonly IMapper _mapper;

    public CreateGitAccessTokenQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IPasswordHasherService passwordHasherService,
        ISystemClock systemClock,
        IMapper mapper
    )
    {
        _contextFactory = contextFactory;
        _passwordHasherService = passwordHasherService;
        _systemClock = systemClock;
        _mapper = mapper;
    }

    public async Task<Option<CreateGitAccessTokenResult>> RunQueryAsync(
        CreateGitAccessTokenQuery query,
        CancellationToken cancellationToken
    )
    {
        if (
            string.IsNullOrWhiteSpace(query.Name)
            || !GitAccessTokenScopes.IsValid(query.Scope)
            || query.OwnerUserId.Value == Guid.Empty
        )
        {
            return Option<CreateGitAccessTokenResult>.None;
        }

        var utcNow = _systemClock.UtcNow;
        DateTimeOffset? expiresAt = query.NeverExpires
            ? null
            : query.ExpiresAt ?? utcNow.Add(GitAccessTokenUtility.DefaultLifetime);

        if (expiresAt is not null && expiresAt <= utcNow)
        {
            return Option<CreateGitAccessTokenResult>.None;
        }

        var token = GitAccessTokenUtility.GenerateToken();
        var entity = new GitAccessTokenEntity
        {
            Id = Guid.NewGuid(),
            OwnerUserId = query.OwnerUserId.Value,
            Name = query.Name.Trim(),
            TokenLookupHash = GitAccessTokenUtility.ComputeLookupHash(token),
            TokenHash = _passwordHasherService.HashPassword(token),
            Scope = query.Scope,
            CreatedAt = utcNow,
            ExpiresAt = expiresAt,
        };

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        context.Set<GitAccessTokenEntity>().Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        var metadata = _mapper.Map<GitAccessTokenDto>(entity);
        return Option.From(
            new CreateGitAccessTokenResult
            {
                Id = GitAccessTokenId.From(entity.Id),
                Token = token,
                Metadata = metadata,
            }
        );
    }
}
