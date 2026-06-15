using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Contracts.Queries.Users;
using OpenGitBase.Features.Users.QueryHandlers.Users;

namespace OpenGitBase.Features.Users.Tests.QueryHandlers;

public class UserGetEmailVerifiedQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenCredentialsExist_ReturnsEmailVerifiedStatus()
    {
        var (provider, connection) = await UsersTestFixture.CreateAsync();
        await using (provider)
        await using (connection)
        {
            var contextFactory = provider.GetRequiredService<
                IDbContextFactory<OpenGitBaseDbContext>
            >();
            var userId = await UsersTestFixture.SeedUserAsync(contextFactory, "statususer");
            await UsersTestFixture.SeedCredentialsAsync(
                contextFactory,
                userId,
                "statususer",
                emailVerified: true
            );

            var handler = provider.GetRequiredService<UserGetEmailVerifiedQueryHandler>();
            var result = await handler.RunQueryAsync(
                new UserGetEmailVerifiedQuery { UserId = UserId.From(userId) },
                CancellationToken.None
            );

            Assert.True(result.IsSome);
            Assert.True(result.Get());
        }
    }

    [Fact]
    public async Task RunQueryAsync_WhenCredentialsMissing_ReturnsNone()
    {
        var (provider, connection) = await UsersTestFixture.CreateAsync();
        await using (provider)
        await using (connection)
        {
            var handler = provider.GetRequiredService<UserGetEmailVerifiedQueryHandler>();
            var result = await handler.RunQueryAsync(
                new UserGetEmailVerifiedQuery { UserId = UserId.From(Guid.NewGuid()) },
                CancellationToken.None
            );

            Assert.True(result.IsNone);
        }
    }
}
