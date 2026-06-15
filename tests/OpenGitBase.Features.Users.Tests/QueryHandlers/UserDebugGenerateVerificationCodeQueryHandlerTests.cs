using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Contracts.Queries.Users;
using OpenGitBase.Features.Users.QueryHandlers.Users;

namespace OpenGitBase.Features.Users.Tests.QueryHandlers;

public class UserDebugGenerateVerificationCodeQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenUnverified_ReturnsPlainCode()
    {
        var (provider, connection) = await UsersTestFixture.CreateAsync();
        await using (provider)
        await using (connection)
        {
            var emailProtection = provider.GetRequiredService<IEmailProtectionService>();
            var contextFactory = provider.GetRequiredService<
                IDbContextFactory<OpenGitBaseDbContext>
            >();
            var userId = await UsersTestFixture.SeedUserAsync(contextFactory, "debug-code");
            await UsersTestFixture.SeedCredentialsAsync(
                contextFactory,
                userId,
                "debug-code",
                emailCiphertext: emailProtection.EncryptEmail("debug-code@example.com"),
                emailLookupHash: emailProtection.ComputeLookupHash("debug-code@example.com")
            );

            var handler = provider.GetRequiredService<UserDebugGenerateVerificationCodeQueryHandler>();
            var result = await handler.RunQueryAsync(
                new UserDebugGenerateVerificationCodeQuery { UserId = UserId.From(userId) },
                CancellationToken.None
            );

            Assert.True(result.IsSome);
            var code = result.Get();
            Assert.Matches(@"^\d{3}-\d{3}-\d{3}$", code.Code);
            Assert.True(code.ExpiresAt > DateTimeOffset.UtcNow);
        }
    }
}
