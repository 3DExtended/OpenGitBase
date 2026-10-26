using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Common.Tests.Testing;
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

    [Fact]
    public async Task RunQueryAsync_StoresTheCredentialsOnceWhenTheCommitAcknowledgementIsLost()
    {
        // The credentials' key is UserId; their Id is a computed alias the database does not know.
        var lostCommit = new LostCommitSimulator();
        var (provider, connection) = await UsersTestFixture.CreateAsync(configureDbContext: lostCommit.Configure);
        await using (provider)
        await using (connection)
        {
            var contextFactory = provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
            var userId = await UsersTestFixture.SeedUserAsync(contextFactory, "creduser");
            var emailProtection = provider.GetRequiredService<IEmailProtectionService>();

            lostCommit.LoseNextCommitInserting("UserCredentials");
            var result = await provider
                .GetRequiredService<UserCredentialsCreateQueryHandler>()
                .RunQueryAsync(
                    new UserCredentialsCreateQuery
                    {
                        ModelToCreate = new UserCredentials
                        {
                            Id = UserCredentialsId.From(userId),
                            Username = "creduser",
                            SignInProvider = false,
                            EmailCiphertext = emailProtection.EncryptEmail("creduser@example.com"),
                            EmailLookupHash = emailProtection.ComputeLookupHash("creduser@example.com"),
                        },
                    },
                    CancellationToken.None
                );

            Assert.Equal(1, lostCommit.LostCommits);
            Assert.Equal(UserCredentialsId.From(userId), result.Get());
            await using var context = await contextFactory.CreateDbContextAsync();
            var credentials = Assert.Single(await context.Set<Entities.UserCredentialsEntity>().ToListAsync());
            Assert.Equal(userId, credentials.UserId);
        }
    }
}
