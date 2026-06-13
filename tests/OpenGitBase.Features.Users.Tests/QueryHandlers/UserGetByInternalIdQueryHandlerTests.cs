using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Contracts.Queries.Users;
using OpenGitBase.Features.Users.QueryHandlers.Users;

namespace OpenGitBase.Features.Users.Tests.QueryHandlers;

public class UserGetByInternalIdQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenInternalIdBlank_ReturnsNone()
    {
        var (provider, connection) = await UsersTestFixture.CreateAsync();
        await using (provider)
        await using (connection)
        {
            var handler = provider.GetRequiredService<UserGetByInternalIdQueryHandler>();
            var result = await handler.RunQueryAsync(
                new UserGetByInternalIdQuery { InternalId = "  " },
                CancellationToken.None
            );
            Assert.True(result.IsNone);
        }
    }

    [Fact]
    public async Task RunQueryAsync_WhenCredentialsMissing_ReturnsNone()
    {
        var (provider, connection) = await UsersTestFixture.CreateAsync();
        await using (provider)
        await using (connection)
        {
            var handler = provider.GetRequiredService<UserGetByInternalIdQueryHandler>();
            var result = await handler.RunQueryAsync(
                new UserGetByInternalIdQuery { InternalId = "missing-internal-id" },
                CancellationToken.None
            );
            Assert.True(result.IsNone);
        }
    }

    [Fact]
    public async Task RunQueryAsync_WhenCredentialsExist_ReturnsUserId()
    {
        var (provider, connection) = await UsersTestFixture.CreateAsync();
        await using (provider)
        await using (connection)
        {
            var contextFactory = provider.GetRequiredService<
                IDbContextFactory<OpenGitBaseDbContext>
            >();
            var userId = await UsersTestFixture.SeedUserAsync(contextFactory, "internaluser");
            await UsersTestFixture.SeedCredentialsAsync(
                contextFactory,
                userId,
                "internaluser",
                internalId: "google-sub-123"
            );

            var handler = provider.GetRequiredService<UserGetByInternalIdQueryHandler>();
            var result = await handler.RunQueryAsync(
                new UserGetByInternalIdQuery { InternalId = "google-sub-123" },
                CancellationToken.None
            );

            Assert.True(result.IsSome);
            Assert.Equal(UserId.From(userId), result.Get());
        }
    }
}
