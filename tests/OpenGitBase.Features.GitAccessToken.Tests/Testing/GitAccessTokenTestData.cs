using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Features.GitAccessToken.Contracts;
using OpenGitBase.Features.GitAccessToken.Entities;
using OpenGitBase.Features.GitAccessToken.QueryHandlers;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.GitAccessToken.Tests.Testing;

public static class GitAccessTokenTestData
{
    public const string SampleName = "CI laptop";

    public const string SampleToken = "ogb_test-token-value";

    public static readonly UserId OwnerUserId = UserId.From(Guid.Parse("11111111-1111-1111-1111-111111111111"));

    public static UserEntity CreateOwner() =>
        new()
        {
            Id = OwnerUserId.Value,
            Username = "token-owner",
            NormalizedUsername = "token-owner",
            CreatedAt = DateTimeOffset.UtcNow,
        };

    public static async Task<(GitAccessTokenId Id, GitAccessTokenEntity Entity)> SeedAsync(
        OpenGitBaseDbContext context,
        IPasswordHasherService? passwordHasher = null,
        string? token = null,
        string scope = GitAccessTokenScopes.Write,
        DateTimeOffset? expiresAt = null,
        DateTimeOffset? revokedAt = null
    )
    {
        passwordHasher ??= new PasswordHasherService();
        token ??= SampleToken;
        var id = Guid.NewGuid();
        var entity = new GitAccessTokenEntity
        {
            Id = id,
            OwnerUserId = OwnerUserId.Value,
            Name = SampleName,
            TokenLookupHash = GitAccessTokenUtility.ComputeLookupHash(token),
            TokenHash = passwordHasher.HashPassword(token),
            Scope = scope,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt ?? DateTimeOffset.UtcNow.AddDays(90),
            RevokedAt = revokedAt,
        };

        if (!await context.Set<UserEntity>().AnyAsync(user => user.Id == OwnerUserId.Value))
        {
            context.Set<UserEntity>().Add(CreateOwner());
        }

        context.Set<GitAccessTokenEntity>().Add(entity);
        await context.SaveChangesAsync().ConfigureAwait(false);
        return (GitAccessTokenId.From(id), entity);
    }
}
