using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.SendGrid;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Queries.Users;
using OpenGitBase.Features.Users.QueryHandlers.Users;

namespace OpenGitBase.Features.Users.Tests.QueryHandlers;

public class UserVerifyEmailQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenTokenValid_SetsEmailVerified()
    {
        const string verificationCode = "123-456-789";
        var (provider, connection) = await UsersTestFixture.CreateAsync();
        await using (provider)
        await using (connection)
        {
            var hasher = provider.GetRequiredService<IPasswordHasherService>();
            var contextFactory = provider.GetRequiredService<
                IDbContextFactory<OpenGitBaseDbContext>
            >();
            var userId = await UsersTestFixture.SeedUserAsync(contextFactory, "verifyuser");
            await UsersTestFixture.SeedCredentialsAsync(
                contextFactory,
                userId,
                "verifyuser",
                emailVerificationTokenHash: hasher.HashPassword(verificationCode),
                emailVerificationTokenExpireDate: DateTimeOffset.UtcNow.AddHours(24)
            );

            var handler = provider.GetRequiredService<UserVerifyEmailQueryHandler>();
            var result = await handler.RunQueryAsync(
                new UserVerifyEmailQuery
                {
                    Username = "verifyuser",
                    VerificationToken = verificationCode,
                },
                CancellationToken.None
            );

            Assert.True(result.IsSome);

            await using var context = await contextFactory.CreateDbContextAsync();
            var credentials = await context
                .Set<OpenGitBase.Features.Users.Entities.UserCredentialsEntity>()
                .SingleAsync(x => x.Username == "verifyuser");
            Assert.True(credentials.EmailVerified);
        }
    }

    [Fact]
    public async Task RunQueryAsync_WhenTokenInvalid_ReturnsNone()
    {
        var (provider, connection) = await UsersTestFixture.CreateAsync();
        await using (provider)
        await using (connection)
        {
            var hasher = provider.GetRequiredService<IPasswordHasherService>();
            var contextFactory = provider.GetRequiredService<
                IDbContextFactory<OpenGitBaseDbContext>
            >();
            var userId = await UsersTestFixture.SeedUserAsync(contextFactory, "badverify");
            await UsersTestFixture.SeedCredentialsAsync(
                contextFactory,
                userId,
                "badverify",
                emailVerificationTokenHash: hasher.HashPassword("123-456-789"),
                emailVerificationTokenExpireDate: DateTimeOffset.UtcNow.AddHours(24)
            );

            var handler = provider.GetRequiredService<UserVerifyEmailQueryHandler>();
            var result = await handler.RunQueryAsync(
                new UserVerifyEmailQuery
                {
                    Username = "badverify",
                    VerificationToken = "000-000-000",
                },
                CancellationToken.None
            );

            Assert.True(result.IsNone);
        }
    }
}
