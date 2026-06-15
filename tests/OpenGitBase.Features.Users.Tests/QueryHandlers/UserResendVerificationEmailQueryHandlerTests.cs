using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.SendGrid;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Contracts.Queries.Users;
using OpenGitBase.Features.Users.QueryHandlers.Users;

namespace OpenGitBase.Features.Users.Tests.QueryHandlers;

public class UserResendVerificationEmailQueryHandlerTests
{
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
            var userId = await UsersTestFixture.SeedUserAsync(contextFactory, "verified");
            await UsersTestFixture.SeedCredentialsAsync(
                contextFactory,
                userId,
                "verified",
                emailCiphertext: emailProtection.EncryptEmail("verified@example.com"),
                emailLookupHash: emailProtection.ComputeLookupHash("verified@example.com"),
                emailVerified: true
            );

            var handler = provider.GetRequiredService<UserResendVerificationEmailQueryHandler>();
            var result = await handler.RunQueryAsync(
                new UserResendVerificationEmailQuery { UserId = UserId.From(userId) },
                CancellationToken.None
            );

            Assert.True(result.IsNone);
        }
    }

    [Fact]
    public async Task RunQueryAsync_WhenValid_SendsEmail()
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
            var contextFactory = provider.GetRequiredService<
                IDbContextFactory<OpenGitBaseDbContext>
            >();
            var userId = await UsersTestFixture.SeedUserAsync(contextFactory, "resend");
            await UsersTestFixture.SeedCredentialsAsync(
                contextFactory,
                userId,
                "resend",
                emailCiphertext: emailProtection.EncryptEmail("resend@example.com"),
                emailLookupHash: emailProtection.ComputeLookupHash("resend@example.com")
            );

            var handler = provider.GetRequiredService<UserResendVerificationEmailQueryHandler>();
            var result = await handler.RunQueryAsync(
                new UserResendVerificationEmailQuery { UserId = UserId.From(userId) },
                CancellationToken.None
            );

            Assert.True(result.IsSome);
            await queryProcessor
                .Received(1)
                .RunQueryAsync(Arg.Any<EmailSendQuery>(), Arg.Any<CancellationToken>());
        }
    }
}
