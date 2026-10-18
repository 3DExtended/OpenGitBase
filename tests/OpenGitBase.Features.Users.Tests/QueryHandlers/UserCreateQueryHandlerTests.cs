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

public class UserCreateQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_CreatesUserWithoutCredentials()
    {
        var fixedNow = new DateTimeOffset(2026, 6, 13, 12, 0, 0, TimeSpan.Zero);
        var (provider, connection) = await UsersTestFixture.CreateAsync(utcNow: fixedNow);
        await using (provider)
        await using (connection)
        {
            var handler = provider.GetRequiredService<UserCreateQueryHandler>();
            var result = await handler.RunQueryAsync(
                new UserCreateQuery { ModelToCreate = new User { Username = "  NewUser  " } },
                CancellationToken.None
            );

            Assert.True(result.IsSome);
            var contextFactory = provider.GetRequiredService<
                IDbContextFactory<OpenGitBaseDbContext>
            >();
            await using var context = await contextFactory.CreateDbContextAsync();
            var user = await context.Set<Entities.UserEntity>().FindAsync(result.Get().Value);
            Assert.NotNull(user);
            Assert.Equal("  NewUser  ", user.Username);
            Assert.Equal("newuser", user.NormalizedUsername);
            Assert.Equal(fixedNow, user.CreatedAt);
        }
    }

    [Fact]
    public async Task RunQueryAsync_CreatesUserWithCredentials()
    {
        var (provider, connection) = await UsersTestFixture.CreateAsync();
        await using (provider)
        await using (connection)
        {
            var hasher = provider.GetRequiredService<IPasswordHasherService>();
            var emailProtection = provider.GetRequiredService<IEmailProtectionService>();
            var handler = provider.GetRequiredService<UserCreateQueryHandler>();
            var result = await handler.RunQueryAsync(
                new UserCreateQuery
                {
                    ModelToCreate = new User { Username = "withcreds" },
                    UserCredentials = new UserCredentials
                    {
                        Username = "withcreds",
                        PasswordHash = hasher.HashPassword("Password123!"),
                        SignInProvider = false,
                        EmailCiphertext = emailProtection.EncryptEmail("withcreds@example.com"),
                        EmailLookupHash = emailProtection.ComputeLookupHash("withcreds@example.com"),
                    },
                },
                CancellationToken.None
            );

            Assert.True(result.IsSome);
            var contextFactory = provider.GetRequiredService<
                IDbContextFactory<OpenGitBaseDbContext>
            >();
            await using var context = await contextFactory.CreateDbContextAsync();
            var credentials = await context
                .Set<Entities.UserCredentialsEntity>()
                .FindAsync(result.Get().Value);
            Assert.NotNull(credentials);
            Assert.Equal("withcreds", credentials.Username);
        }
    }

    [Fact]
    public async Task RunQueryAsync_CreatesTheUserOnceWhenTheCommitAcknowledgementIsLost()
    {
        var lostCommit = new LostCommitSimulator();
        var (provider, connection) = await UsersTestFixture.CreateAsync(configureDbContext: lostCommit.Configure);
        await using (provider)
        await using (connection)
        {
            var emailProtection = provider.GetRequiredService<IEmailProtectionService>();
            lostCommit.LoseNextCommitInserting("Users");
            var result = await provider
                .GetRequiredService<UserCreateQueryHandler>()
                .RunQueryAsync(
                    new UserCreateQuery
                    {
                        ModelToCreate = new User { Username = "retried" },
                        UserCredentials = new UserCredentials
                        {
                            Username = "retried",
                            SignInProvider = false,
                            EmailCiphertext = emailProtection.EncryptEmail("retried@example.com"),
                            EmailLookupHash = emailProtection.ComputeLookupHash("retried@example.com"),
                        },
                    },
                    CancellationToken.None
                );

            Assert.Equal(1, lostCommit.LostCommits);
            Assert.True(result.IsSome);
            var contextFactory = provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            var user = Assert.Single(await context.Set<Entities.UserEntity>().ToListAsync());
            Assert.Equal(result.Get().Value, user.Id);
            var credentials = Assert.Single(await context.Set<Entities.UserCredentialsEntity>().ToListAsync());
            Assert.Equal(user.Id, credentials.UserId);
        }
    }
}
