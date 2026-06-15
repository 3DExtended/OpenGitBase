using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Contracts.Queries.Users;
using OpenGitBase.Features.Users.QueryHandlers.Users;

namespace OpenGitBase.Features.Users.Tests.QueryHandlers;

public class UserDebugVerifyEmailQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenUnverified_SetsEmailVerified()
    {
        var (provider, connection) = await UsersTestFixture.CreateAsync();
        await using (provider)
        await using (connection)
        {
            var emailProtection = provider.GetRequiredService<IEmailProtectionService>();
            var contextFactory = provider.GetRequiredService<
                IDbContextFactory<OpenGitBaseDbContext>
            >();
            var userId = await UsersTestFixture.SeedUserAsync(contextFactory, "debug-verify");
            await UsersTestFixture.SeedCredentialsAsync(
                contextFactory,
                userId,
                "debug-verify",
                emailCiphertext: emailProtection.EncryptEmail("debug-verify@example.com"),
                emailLookupHash: emailProtection.ComputeLookupHash("debug-verify@example.com")
            );

            var handler = provider.GetRequiredService<UserDebugVerifyEmailQueryHandler>();
            var result = await handler.RunQueryAsync(
                new UserDebugVerifyEmailQuery { UserId = UserId.From(userId) },
                CancellationToken.None
            );

            Assert.True(result.IsSome);

            await using var context = await contextFactory.CreateDbContextAsync();
            var credentials = await context
                .Set<OpenGitBase.Features.Users.Entities.UserCredentialsEntity>()
                .SingleAsync(x => x.Username == "debug-verify");
            Assert.True(credentials.EmailVerified);
            Assert.Null(credentials.EmailVerificationTokenHash);
        }
    }

    [Fact]
    public async Task RunQueryAsync_WhenAlreadyVerified_ReturnsNone()
    {
        var (provider, connection) = await UsersTestFixture.CreateAsync();
        await using (provider)
        await using (connection)
        {
            var emailProtection = provider.GetRequiredService<IEmailProtectionService>();
            var contextFactory = provider.GetRequiredService<
                IDbContextFactory<OpenGitBaseDbContext>
            >();
            var userId = await UsersTestFixture.SeedUserAsync(contextFactory, "debug-verified");
            await UsersTestFixture.SeedCredentialsAsync(
                contextFactory,
                userId,
                "debug-verified",
                emailCiphertext: emailProtection.EncryptEmail("debug-verified@example.com"),
                emailLookupHash: emailProtection.ComputeLookupHash("debug-verified@example.com"),
                emailVerified: true
            );

            var handler = provider.GetRequiredService<UserDebugVerifyEmailQueryHandler>();
            var result = await handler.RunQueryAsync(
                new UserDebugVerifyEmailQuery { UserId = UserId.From(userId) },
                CancellationToken.None
            );

            Assert.True(result.IsNone);
        }
    }
}
