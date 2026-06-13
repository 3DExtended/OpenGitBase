using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Contracts.Queries.Users;
using OpenGitBase.Features.Users.QueryHandlers.Users;

namespace OpenGitBase.Features.Users.Tests.QueryHandlers;

public class UserGetByIdQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenUserMissing_ReturnsNone()
    {
        var (provider, connection) = await UsersTestFixture.CreateAsync();
        await using (provider)
        await using (connection)
        {
            var handler = provider.GetRequiredService<UserGetByIdQueryHandler>();
            var result = await handler.RunQueryAsync(
                new UserGetByIdQuery { ModelId = UserId.From(Guid.NewGuid()) },
                CancellationToken.None
            );
            Assert.True(result.IsNone);
        }
    }

    [Fact]
    public async Task RunQueryAsync_WhenUserExists_ReturnsUser()
    {
        var (provider, connection) = await UsersTestFixture.CreateAsync();
        await using (provider)
        await using (connection)
        {
            var contextFactory = provider.GetRequiredService<
                IDbContextFactory<OpenGitBaseDbContext>
            >();
            var userId = await UsersTestFixture.SeedUserAsync(contextFactory, "lookupuser");

            var handler = provider.GetRequiredService<UserGetByIdQueryHandler>();
            var result = await handler.RunQueryAsync(
                new UserGetByIdQuery { ModelId = UserId.From(userId) },
                CancellationToken.None
            );

            Assert.True(result.IsSome);
            Assert.Equal("lookupuser", result.Get().Username);
        }
    }
}
