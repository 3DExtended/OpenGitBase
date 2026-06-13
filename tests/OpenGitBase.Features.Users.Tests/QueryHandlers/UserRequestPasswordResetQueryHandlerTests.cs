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

public class UserRequestPasswordResetQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenCredentialsMissing_ReturnsNone()
    {
        var (provider, connection) = await UsersTestFixture.CreateAsync();
        await using (provider)
        await using (connection)
        {
            var handler = provider.GetRequiredService<UserRequestPasswordResetQueryHandler>();
            var result = await handler.RunQueryAsync(
                new UserRequestPasswordResetQuery
                {
                    Username = "missing",
                    Email = "missing@example.com",
                },
                CancellationToken.None
            );
            Assert.True(result.IsNone);
        }
    }

    [Fact]
    public async Task RunQueryAsync_WhenEmailMismatch_ReturnsNone()
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
            var userId = await UsersTestFixture.SeedUserAsync(contextFactory, "resetuser");
            await UsersTestFixture.SeedCredentialsAsync(
                contextFactory,
                userId,
                "resetuser",
                passwordHash: hasher.HashPassword("Password123!"),
                emailCiphertext: emailProtection.EncryptEmail("real@example.com"),
                emailLookupHash: emailProtection.ComputeLookupHash("real@example.com")
            );

            var handler = provider.GetRequiredService<UserRequestPasswordResetQueryHandler>();
            var result = await handler.RunQueryAsync(
                new UserRequestPasswordResetQuery
                {
                    Username = "resetuser",
                    Email = "wrong@example.com",
                },
                CancellationToken.None
            );
            Assert.True(result.IsNone);
        }
    }

    [Fact]
    public async Task RunQueryAsync_WhenEmailSendFails_ReturnsNone()
    {
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<EmailSendQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<Unit>.None);

        var (provider, connection) = await UsersTestFixture.CreateAsync(services =>
        {
            services.RemoveAll<IQueryProcessor>();
            services.AddSingleton(queryProcessor);
        });
        await using (provider)
        await using (connection)
        {
            var emailProtection = provider.GetRequiredService<IEmailProtectionService>();
            var hasher = provider.GetRequiredService<IPasswordHasherService>();
            var contextFactory = provider.GetRequiredService<
                IDbContextFactory<OpenGitBaseDbContext>
            >();
            var userId = await UsersTestFixture.SeedUserAsync(contextFactory, "sendfail");
            await UsersTestFixture.SeedCredentialsAsync(
                contextFactory,
                userId,
                "sendfail",
                passwordHash: hasher.HashPassword("Password123!"),
                emailCiphertext: emailProtection.EncryptEmail("sendfail@example.com"),
                emailLookupHash: emailProtection.ComputeLookupHash("sendfail@example.com")
            );

            var handler = provider.GetRequiredService<UserRequestPasswordResetQueryHandler>();
            var result = await handler.RunQueryAsync(
                new UserRequestPasswordResetQuery
                {
                    Username = "sendfail",
                    Email = "sendfail@example.com",
                },
                CancellationToken.None
            );
            Assert.True(result.IsNone);
        }
    }

    [Fact]
    public async Task RunQueryAsync_WhenValid_SendsEmailAndReturnsUnit()
    {
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<EmailSendQuery>(), Arg.Any<CancellationToken>())
            .Returns(Unit.Value);

        var (provider, connection) = await UsersTestFixture.CreateAsync(services =>
        {
            services.RemoveAll<IQueryProcessor>();
            services.AddSingleton(queryProcessor);
        });
        await using (provider)
        await using (connection)
        {
            var emailProtection = provider.GetRequiredService<IEmailProtectionService>();
            var hasher = provider.GetRequiredService<IPasswordHasherService>();
            var contextFactory = provider.GetRequiredService<
                IDbContextFactory<OpenGitBaseDbContext>
            >();
            var userId = await UsersTestFixture.SeedUserAsync(contextFactory, "validreset");
            await UsersTestFixture.SeedCredentialsAsync(
                contextFactory,
                userId,
                "validreset",
                passwordHash: hasher.HashPassword("Password123!"),
                emailCiphertext: emailProtection.EncryptEmail("validreset@example.com"),
                emailLookupHash: emailProtection.ComputeLookupHash("validreset@example.com")
            );

            var handler = provider.GetRequiredService<UserRequestPasswordResetQueryHandler>();
            var result = await handler.RunQueryAsync(
                new UserRequestPasswordResetQuery
                {
                    Username = "validreset",
                    Email = "validreset@example.com",
                },
                CancellationToken.None
            );

            Assert.True(result.IsSome);
            await queryProcessor
                .Received(1)
                .RunQueryAsync(Arg.Any<EmailSendQuery>(), Arg.Any<CancellationToken>());
        }
    }
}
