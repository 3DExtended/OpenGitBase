using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Contracts.Queries.Users;
using OpenGitBase.Features.Users.QueryHandlers.Users;

namespace OpenGitBase.Features.Users.Tests.QueryHandlers;

public class UserCredentialsCreateQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_PersistsCredentials()
    {
        var (provider, connection) = await UsersTestFixture.CreateAsync();
        await using (provider)
        await using (connection)
        {
            var contextFactory = provider.GetRequiredService<
                IDbContextFactory<OpenGitBaseDbContext>
            >();
            var userId = await UsersTestFixture.SeedUserAsync(contextFactory, "creduser");
            var hasher = provider.GetRequiredService<IPasswordHasherService>();
            var emailProtection = provider.GetRequiredService<IEmailProtectionService>();

            var handler = provider.GetRequiredService<UserCredentialsCreateQueryHandler>();
            var result = await handler.RunQueryAsync(
                new UserCredentialsCreateQuery
                {
                    ModelToCreate = new UserCredentials
                    {
                        Id = UserCredentialsId.From(userId),
                        Username = "creduser",
                        PasswordHash = hasher.HashPassword("Password123!"),
                        SignInProvider = false,
                        EmailCiphertext = emailProtection.EncryptEmail("creduser@example.com"),
                        EmailLookupHash = emailProtection.ComputeLookupHash("creduser@example.com"),
                    },
                },
                CancellationToken.None
            );

            Assert.True(result.IsSome);
            Assert.Equal(UserCredentialsId.From(userId), result.Get());
        }
    }
}
