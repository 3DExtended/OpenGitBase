using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Contracts.Queries.Users;
using OpenGitBase.Features.Users.QueryHandlers.Users;

namespace OpenGitBase.Features.Users.Tests.QueryHandlers;

public class UserGetIdByEmailQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenEmailBlank_ReturnsNone()
    {
        var (provider, connection) = await UsersTestFixture.CreateAsync();
        await using (provider)
        await using (connection)
        {
            var handler = provider.GetRequiredService<UserGetIdByEmailQueryHandler>();
            var result = await handler.RunQueryAsync(
                new UserGetIdByEmailQuery { Email = string.Empty },
                CancellationToken.None
            );
            Assert.True(result.IsNone);
        }
    }

    [Fact]
    public async Task RunQueryAsync_WhenEmailExists_ReturnsUserId()
    {
        var (provider, connection) = await UsersTestFixture.CreateAsync();
        await using (provider)
        await using (connection)
        {
            var emailProtection = provider.GetRequiredService<IEmailProtectionService>();
            var contextFactory = provider.GetRequiredService<
                IDbContextFactory<OpenGitBaseDbContext>
            >();
            var userId = await UsersTestFixture.SeedUserAsync(contextFactory, "emailuser");
            await UsersTestFixture.SeedCredentialsAsync(
                contextFactory,
                userId,
                "emailuser",
                emailCiphertext: emailProtection.EncryptEmail("found@example.com"),
                emailLookupHash: emailProtection.ComputeLookupHash("found@example.com")
            );

            var handler = provider.GetRequiredService<UserGetIdByEmailQueryHandler>();
            var result = await handler.RunQueryAsync(
                new UserGetIdByEmailQuery { Email = "found@example.com" },
                CancellationToken.None
            );

            Assert.True(result.IsSome);
            Assert.Equal(userId, result.Get().Value);
        }
    }

    [Fact]
    public async Task RunQueryAsync_WhenEmailMissing_ReturnsNone()
    {
        var (provider, connection) = await UsersTestFixture.CreateAsync();
        await using (provider)
        await using (connection)
        {
            var handler = provider.GetRequiredService<UserGetIdByEmailQueryHandler>();
            var result = await handler.RunQueryAsync(
                new UserGetIdByEmailQuery { Email = "missing@example.com" },
                CancellationToken.None
            );
            Assert.True(result.IsNone);
        }
    }
}
