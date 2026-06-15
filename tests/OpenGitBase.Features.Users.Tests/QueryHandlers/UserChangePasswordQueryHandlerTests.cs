using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Contracts.Queries.Users;
using OpenGitBase.Features.Users.QueryHandlers.Users;

namespace OpenGitBase.Features.Users.Tests.QueryHandlers;

public class UserChangePasswordQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenCurrentPasswordValid_ChangesPassword()
    {
        var (provider, connection) = await UsersTestFixture.CreateAsync();
        await using (provider)
        await using (connection)
        {
            var hasher = provider.GetRequiredService<IPasswordHasherService>();
            var contextFactory = provider.GetRequiredService<
                IDbContextFactory<OpenGitBaseDbContext>
            >();
            var userId = await UsersTestFixture.SeedUserAsync(contextFactory, "changepass");
            await UsersTestFixture.SeedCredentialsAsync(
                contextFactory,
                userId,
                "changepass",
                passwordHash: hasher.HashPassword("OldPassword123!")
            );

            var handler = provider.GetRequiredService<UserChangePasswordQueryHandler>();
            var result = await handler.RunQueryAsync(
                new UserChangePasswordQuery
                {
                    UserId = UserId.From(userId),
                    CurrentPassword = "OldPassword123!",
                    NewPassword = "NewPassword456!",
                },
                CancellationToken.None
            );

            Assert.True(result.IsSome);

            await using var context = await contextFactory.CreateDbContextAsync();
            var credentials = await context
                .Set<OpenGitBase.Features.Users.Entities.UserCredentialsEntity>()
                .SingleAsync(x => x.Username == "changepass");
            Assert.True(hasher.VerifyPassword(credentials.PasswordHash!, "NewPassword456!"));
        }
    }

    [Fact]
    public async Task RunQueryAsync_WhenCurrentPasswordInvalid_ReturnsNone()
    {
        var (provider, connection) = await UsersTestFixture.CreateAsync();
        await using (provider)
        await using (connection)
        {
            var hasher = provider.GetRequiredService<IPasswordHasherService>();
            var contextFactory = provider.GetRequiredService<
                IDbContextFactory<OpenGitBaseDbContext>
            >();
            var userId = await UsersTestFixture.SeedUserAsync(contextFactory, "badpass");
            await UsersTestFixture.SeedCredentialsAsync(
                contextFactory,
                userId,
                "badpass",
                passwordHash: hasher.HashPassword("OldPassword123!")
            );

            var handler = provider.GetRequiredService<UserChangePasswordQueryHandler>();
            var result = await handler.RunQueryAsync(
                new UserChangePasswordQuery
                {
                    UserId = UserId.From(userId),
                    CurrentPassword = "WrongPassword!",
                    NewPassword = "NewPassword456!",
                },
                CancellationToken.None
            );

            Assert.True(result.IsNone);
        }
    }
}
