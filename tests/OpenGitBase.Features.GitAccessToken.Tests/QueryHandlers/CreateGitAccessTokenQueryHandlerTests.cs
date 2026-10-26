using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.GitAccessToken;
using OpenGitBase.Features.GitAccessToken.Contracts;
using OpenGitBase.Features.GitAccessToken.QueryHandlers;
using OpenGitBase.Features.GitAccessToken.Tests.Testing;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.GitAccessToken.Tests.QueryHandlers;

public class CreateGitAccessTokenQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_PersistsHashedToken_ReturnsPlaintextOnce()
    {
        await using var scope = new GitAccessTokenHandlerTestScope();
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        seedContext.Set<OpenGitBase.Features.Users.Entities.UserEntity>().Add(GitAccessTokenTestData.CreateOwner());
        await seedContext.SaveChangesAsync();

        var handler = scope.GetHandler<CreateGitAccessTokenQueryHandler>();
        var result = await handler.RunQueryAsync(
            new CreateGitAccessTokenQuery
            {
                OwnerUserId = GitAccessTokenTestData.OwnerUserId,
                Name = GitAccessTokenTestData.SampleName,
                Scope = GitAccessTokenScopes.Write,
            },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertSome(
            result,
            created =>
            {
                Assert.StartsWith("ogb_", created.Token, StringComparison.Ordinal);
                Assert.Equal(GitAccessTokenScopes.Write, created.Metadata.Scope);
                Assert.Equal(GitAccessTokenTestData.SampleName, created.Metadata.Name);
            }
        );

        await using var verifyContext = await scope.CreateDbContextAsync();
        var entity = await verifyContext
            .Set<OpenGitBase.Features.GitAccessToken.Entities.GitAccessTokenEntity>()
            .FindAsync(result.Get().Id.Value);
        Assert.NotNull(entity);
        Assert.NotEqual(result.Get().Token, entity.TokenHash);
        Assert.DoesNotContain(result.Get().Token, entity.TokenHash, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunQueryAsync_StoresTheTokenOnceWhenTheCommitAcknowledgementIsLost()
    {
        var lostCommit = new LostCommitSimulator();
        await using var scope = new GitAccessTokenHandlerTestScope(lostCommit.Configure);
        await scope.EnsureCreatedAsync();

        await using (var seedContext = await scope.CreateDbContextAsync())
        {
            seedContext.Set<OpenGitBase.Features.Users.Entities.UserEntity>().Add(GitAccessTokenTestData.CreateOwner());
            await seedContext.SaveChangesAsync();
        }

        lostCommit.LoseNextCommitInserting("GitAccessToken");
        var result = await scope
            .GetHandler<CreateGitAccessTokenQueryHandler>()
            .RunQueryAsync(
                new CreateGitAccessTokenQuery
                {
                    OwnerUserId = GitAccessTokenTestData.OwnerUserId,
                    Name = GitAccessTokenTestData.SampleName,
                    Scope = GitAccessTokenScopes.Write,
                },
                CancellationToken.None
            );

        Assert.Equal(1, lostCommit.LostCommits);
        var created = QueryHandlerResultAssert.AssertSome(result);
        await using var verifyContext = await scope.CreateDbContextAsync();
        var entity = Assert.Single(
            await verifyContext
                .Set<OpenGitBase.Features.GitAccessToken.Entities.GitAccessTokenEntity>()
                .ToListAsync()
        );
        Assert.Equal(created.Id.Value, entity.Id);
        Assert.True(new PasswordHasherService().VerifyPassword(entity.TokenHash, created.Token));
    }

    [Fact]
    public async Task RunQueryAsync_WhenScopeInvalid_ReturnsNone()
    {
        await using var scope = new GitAccessTokenHandlerTestScope();
        await scope.EnsureCreatedAsync();

        var handler = scope.GetHandler<CreateGitAccessTokenQueryHandler>();
        var result = await handler.RunQueryAsync(
            new CreateGitAccessTokenQuery
            {
                OwnerUserId = GitAccessTokenTestData.OwnerUserId,
                Name = GitAccessTokenTestData.SampleName,
                Scope = "admin",
            },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertNone(result);
    }
}
