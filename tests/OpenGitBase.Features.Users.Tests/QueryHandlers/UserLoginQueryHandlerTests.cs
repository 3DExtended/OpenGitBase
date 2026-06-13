using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Contracts.Queries.Users;
using OpenGitBase.Features.Users.QueryHandlers.Users;

namespace OpenGitBase.Features.Users.Tests.QueryHandlers;

public class UserLoginQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenCredentialsMissing_ReturnsNone()
    {
        var (provider, connection) = await UsersTestFixture.CreateAsync();
        await using (provider)
        await using (connection)
        {
            var handler = provider.GetRequiredService<UserLoginQueryHandler>();
            var result = await handler.RunQueryAsync(
                new UserLoginQuery { Username = "nobody", Password = "Password123!" },
                CancellationToken.None
            );
            Assert.True(result.IsNone);
        }
    }

    [Fact]
    public async Task RunQueryAsync_WhenSignInProvider_ReturnsNone()
    {
        var (provider, connection) = await UsersTestFixture.CreateAsync();
        await using (provider)
        await using (connection)
        {
            var contextFactory = provider.GetRequiredService<
                IDbContextFactory<OpenGitBaseDbContext>
            >();
            var userId = await UsersTestFixture.SeedUserAsync(contextFactory, "googleuser");
            await UsersTestFixture.SeedCredentialsAsync(
                contextFactory,
                userId,
                "googleuser",
                signInProvider: true
            );

            var handler = provider.GetRequiredService<UserLoginQueryHandler>();
            var result = await handler.RunQueryAsync(
                new UserLoginQuery { Username = "googleuser", Password = "anything" },
                CancellationToken.None
            );
            Assert.True(result.IsNone);
        }
    }

    [Fact]
    public async Task RunQueryAsync_WhenPasswordWrong_ReturnsNone()
    {
        var (provider, connection) = await UsersTestFixture.CreateAsync();
        await using (provider)
        await using (connection)
        {
            var hasher = provider.GetRequiredService<IPasswordHasherService>();
            var contextFactory = provider.GetRequiredService<
                IDbContextFactory<OpenGitBaseDbContext>
            >();
            var userId = await UsersTestFixture.SeedUserAsync(contextFactory, "loginuser");
            await UsersTestFixture.SeedCredentialsAsync(
                contextFactory,
                userId,
                "loginuser",
                passwordHash: hasher.HashPassword("CorrectPassword123!")
            );

            var handler = provider.GetRequiredService<UserLoginQueryHandler>();
            var result = await handler.RunQueryAsync(
                new UserLoginQuery { Username = "loginuser", Password = "WrongPassword123!" },
                CancellationToken.None
            );
            Assert.True(result.IsNone);
        }
    }

    [Fact]
    public async Task RunQueryAsync_WhenPasswordValid_ReturnsUserId()
    {
        var (provider, connection) = await UsersTestFixture.CreateAsync();
        await using (provider)
        await using (connection)
        {
            var hasher = provider.GetRequiredService<IPasswordHasherService>();
            var contextFactory = provider.GetRequiredService<
                IDbContextFactory<OpenGitBaseDbContext>
            >();
            var userId = await UsersTestFixture.SeedUserAsync(contextFactory, "validlogin");
            await UsersTestFixture.SeedCredentialsAsync(
                contextFactory,
                userId,
                "validlogin",
                passwordHash: hasher.HashPassword("Password123!")
            );

            var handler = provider.GetRequiredService<UserLoginQueryHandler>();
            var result = await handler.RunQueryAsync(
                new UserLoginQuery { Username = "validlogin", Password = "Password123!" },
                CancellationToken.None
            );

            Assert.True(result.IsSome);
            Assert.Equal(UserId.From(userId), result.Get());
        }
    }
}
