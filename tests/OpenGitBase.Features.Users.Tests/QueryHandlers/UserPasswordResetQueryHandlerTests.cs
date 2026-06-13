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

public class UserPasswordResetQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenResetTokenMissing_ReturnsNone()
    {
        var (provider, connection) = await UsersTestFixture.CreateAsync();
        await using (provider)
        await using (connection)
        {
            var emailProtection = provider.GetRequiredService<IEmailProtectionService>();
            var hasher = provider.GetRequiredService<IPasswordHasherService>();
            var contextFactory = provider.GetRequiredService<
                IDbContextFactory<OpenGitBaseDbContext>
            >();
            var userId = await UsersTestFixture.SeedUserAsync(contextFactory, "notoken");
            await UsersTestFixture.SeedCredentialsAsync(
                contextFactory,
                userId,
                "notoken",
                passwordHash: hasher.HashPassword("Password123!"),
                emailCiphertext: emailProtection.EncryptEmail("notoken@example.com"),
                emailLookupHash: emailProtection.ComputeLookupHash("notoken@example.com")
            );

            var handler = provider.GetRequiredService<UserPasswordResetQueryHandler>();
            var result = await handler.RunQueryAsync(
                new UserPasswordResetQuery
                {
                    Username = "notoken",
                    Email = "notoken@example.com",
                    ResetCode = "123-456-789",
                    NewPassword = "NewPassword123!",
                },
                CancellationToken.None
            );
            Assert.True(result.IsNone);
        }
    }

    [Fact]
    public async Task RunQueryAsync_WhenResetCodeWrong_ReturnsNone()
    {
        var now = new DateTimeOffset(2026, 6, 13, 12, 0, 0, TimeSpan.Zero);
        var (provider, connection) = await UsersTestFixture.CreateAsync(utcNow: now);
        await using (provider)
        await using (connection)
        {
            var emailProtection = provider.GetRequiredService<IEmailProtectionService>();
            var hasher = provider.GetRequiredService<IPasswordHasherService>();
            var contextFactory = provider.GetRequiredService<
                IDbContextFactory<OpenGitBaseDbContext>
            >();
            var userId = await UsersTestFixture.SeedUserAsync(contextFactory, "badcode");
            await UsersTestFixture.SeedCredentialsAsync(
                contextFactory,
                userId,
                "badcode",
                passwordHash: hasher.HashPassword("Password123!"),
                emailCiphertext: emailProtection.EncryptEmail("badcode@example.com"),
                emailLookupHash: emailProtection.ComputeLookupHash("badcode@example.com"),
                passwordResetTokenHash: hasher.HashPassword("111-222-333"),
                passwordResetTokenExpireDate: now.AddHours(1)
            );

            var handler = provider.GetRequiredService<UserPasswordResetQueryHandler>();
            var result = await handler.RunQueryAsync(
                new UserPasswordResetQuery
                {
                    Username = "badcode",
                    Email = "badcode@example.com",
                    ResetCode = "999-888-777",
                    NewPassword = "NewPassword123!",
                },
                CancellationToken.None
            );
            Assert.True(result.IsNone);
        }
    }

    [Fact]
    public async Task RunQueryAsync_WhenTokenExpired_ReturnsNone()
    {
        var now = new DateTimeOffset(2026, 6, 13, 12, 0, 0, TimeSpan.Zero);
        var (provider, connection) = await UsersTestFixture.CreateAsync(utcNow: now);
        await using (provider)
        await using (connection)
        {
            var emailProtection = provider.GetRequiredService<IEmailProtectionService>();
            var hasher = provider.GetRequiredService<IPasswordHasherService>();
            var contextFactory = provider.GetRequiredService<
                IDbContextFactory<OpenGitBaseDbContext>
            >();
            var userId = await UsersTestFixture.SeedUserAsync(contextFactory, "expired");
            await UsersTestFixture.SeedCredentialsAsync(
                contextFactory,
                userId,
                "expired",
                passwordHash: hasher.HashPassword("Password123!"),
                emailCiphertext: emailProtection.EncryptEmail("expired@example.com"),
                emailLookupHash: emailProtection.ComputeLookupHash("expired@example.com"),
                passwordResetTokenHash: hasher.HashPassword("111-222-333"),
                passwordResetTokenExpireDate: now.AddHours(-1)
            );

            var handler = provider.GetRequiredService<UserPasswordResetQueryHandler>();
            var result = await handler.RunQueryAsync(
                new UserPasswordResetQuery
                {
                    Username = "expired",
                    Email = "expired@example.com",
                    ResetCode = "111-222-333",
                    NewPassword = "NewPassword123!",
                },
                CancellationToken.None
            );
            Assert.True(result.IsNone);
        }
    }

    [Fact]
    public async Task RunQueryAsync_WhenValid_UpdatesPasswordAndSendsEmail()
    {
        var now = new DateTimeOffset(2026, 6, 13, 12, 0, 0, TimeSpan.Zero);
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<EmailSendQuery>(), Arg.Any<CancellationToken>())
            .Returns(Unit.Value);

        var (provider, connection) = await UsersTestFixture.CreateAsync(
            services =>
            {
                services.RemoveAll<IQueryProcessor>();
                services.AddSingleton(queryProcessor);
            },
            utcNow: now
        );
        await using (provider)
        await using (connection)
        {
            var emailProtection = provider.GetRequiredService<IEmailProtectionService>();
            var hasher = provider.GetRequiredService<IPasswordHasherService>();
            var contextFactory = provider.GetRequiredService<
                IDbContextFactory<OpenGitBaseDbContext>
            >();
            var userId = await UsersTestFixture.SeedUserAsync(contextFactory, "goodreset");
            await UsersTestFixture.SeedCredentialsAsync(
                contextFactory,
                userId,
                "goodreset",
                passwordHash: hasher.HashPassword("OldPassword123!"),
                emailCiphertext: emailProtection.EncryptEmail("goodreset@example.com"),
                emailLookupHash: emailProtection.ComputeLookupHash("goodreset@example.com"),
                passwordResetTokenHash: hasher.HashPassword("555-666-777"),
                passwordResetTokenExpireDate: now.AddHours(1)
            );

            var handler = provider.GetRequiredService<UserPasswordResetQueryHandler>();
            var result = await handler.RunQueryAsync(
                new UserPasswordResetQuery
                {
                    Username = "goodreset",
                    Email = "goodreset@example.com",
                    ResetCode = "555-666-777",
                    NewPassword = "NewPassword123!",
                },
                CancellationToken.None
            );

            Assert.True(result.IsSome);
            await using var context = await contextFactory.CreateDbContextAsync();
            var credentials = await context
                .Set<Entities.UserCredentialsEntity>()
                .FindAsync(userId);
            Assert.NotNull(credentials);
            Assert.Null(credentials.PasswordResetTokenHash);
            Assert.Null(credentials.PasswordResetTokenExpireDate);
            Assert.True(hasher.VerifyPassword(credentials.PasswordHash!, "NewPassword123!"));
        }
    }
}
