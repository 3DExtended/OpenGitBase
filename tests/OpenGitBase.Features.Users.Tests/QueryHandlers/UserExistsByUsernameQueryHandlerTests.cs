using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Contracts.Queries.Users;
using OpenGitBase.Features.Users.QueryHandlers.Users;

namespace OpenGitBase.Features.Users.Tests.QueryHandlers;

public class UserExistsByUsernameQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenUsernameBlank_ReturnsNone()
    {
        var (provider, connection) = await UsersTestFixture.CreateAsync();
        await using (provider)
        await using (connection)
        {
            var handler = provider.GetRequiredService<UserExistsByUsernameQueryHandler>();
            var result = await handler.RunQueryAsync(
                new UserExistsByUsernameQuery { Username = "  " },
                CancellationToken.None
            );
            Assert.True(result.IsNone);
        }
    }

    [Fact]
    public async Task RunQueryAsync_WhenUserExists_ReturnsTrue()
    {
        var (provider, connection) = await UsersTestFixture.CreateAsync();
        await using (provider)
        await using (connection)
        {
            var contextFactory = provider.GetRequiredService<
                IDbContextFactory<OpenGitBaseDbContext>
            >();
            await UsersTestFixture.SeedUserAsync(contextFactory, "ExistingUser");

            var handler = provider.GetRequiredService<UserExistsByUsernameQueryHandler>();
            var result = await handler.RunQueryAsync(
                new UserExistsByUsernameQuery { Username = "ExistingUser" },
                CancellationToken.None
            );

            Assert.True(result.IsSome);
            Assert.True(result.Get());
        }
    }

    [Fact]
    public async Task RunQueryAsync_WhenUserMissing_ReturnsFalse()
    {
        var (provider, connection) = await UsersTestFixture.CreateAsync();
        await using (provider)
        await using (connection)
        {
            var handler = provider.GetRequiredService<UserExistsByUsernameQueryHandler>();
            var result = await handler.RunQueryAsync(
                new UserExistsByUsernameQuery { Username = "missing" },
                CancellationToken.None
            );

            Assert.True(result.IsSome);
            Assert.False(result.Get());
        }
    }
}
