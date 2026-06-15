using Mapster;
using MapsterMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Common;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.SendGrid;
using OpenGitBase.Common.SendGrid.QueryHandlers;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.DependencyInjection;
using OpenGitBase.Features.Users;
using OpenGitBase.Features.Users.Contracts.Queries.Users;
using OpenGitBase.Features.Users.QueryHandlers.Users;

namespace OpenGitBase.Features.Users.Tests.QueryHandlers;

public class UserRegisterQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_CreatesUser()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var encryptionOptions = new EncryptionOptions
        {
            DataKey = Convert.ToBase64String(new byte[32]),
            Pepper = "test-pepper",
        };

        var services = new ServiceCollection();
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider([typeof(UsersMapsterConfig).Assembly])
        );
        var mapsterConfig = new TypeAdapterConfig();
        new UsersMapsterConfig().Register(mapsterConfig);
        services.AddSingleton(mapsterConfig);
        services.AddSingleton<IMapper>(sp => new Mapper(
            sp.GetRequiredService<TypeAdapterConfig>()
        ));
        services.AddSingleton(encryptionOptions);
        services.AddSingleton<IEmailProtectionService, EmailProtectionService>();
        services.AddSingleton<IPasswordHasherService, PasswordHasherService>();
        services.AddSingleton<ISystemClock, SystemClock>();
        services.AddSingleton(
            new SendGridOptions
            {
                ApiKey = "test-key",
                FromEmailAddress = "test@example.com",
                FromSenderName = "Test",
                IsDisabled = true,
            }
        );
        services.AddSingleton<ISendGridEmailSender, SendGridEmailSender>();
        services.AddDbContextFactory<OpenGitBaseDbContext>(options =>
            options.UseSqlite(connection)
        );
        services.AddLogging();
        services.AddCqrs(options =>
        {
            options.WithQueryHandlersFrom(
                typeof(UserRegisterQueryHandler).Assembly,
                typeof(EmailSendQueryHandler).Assembly
            );
        });

        await using var serviceProvider = services.BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();

        var contextFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
        }

        var handler = scope.ServiceProvider.GetRequiredService<UserRegisterQueryHandler>();

        var result = await handler.RunQueryAsync(
            new UserRegisterQuery
            {
                Username = "testuser",
                Email = "test@example.com",
                Password = "Password123!",
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
    }

    [Fact]
    public async Task RunQueryAsync_WhenUsernameExists_ReturnsNone()
    {
        var (provider, connection) = await UsersTestFixture.CreateAsync();
        await using (provider)
        await using (connection)
        {
            var contextFactory = provider.GetRequiredService<
                IDbContextFactory<OpenGitBaseDbContext>
            >();
            await UsersTestFixture.SeedUserAsync(contextFactory, "taken");

            var handler = provider.GetRequiredService<UserRegisterQueryHandler>();
            var result = await handler.RunQueryAsync(
                new UserRegisterQuery
                {
                    Username = "taken",
                    Email = "new@example.com",
                    Password = "Password123!",
                },
                CancellationToken.None
            );

            Assert.True(result.IsNone);
        }
    }

    [Fact]
    public async Task RunQueryAsync_WhenEmailExists_ReturnsNone()
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
            var userId = await UsersTestFixture.SeedUserAsync(contextFactory, "existingemail");
            await UsersTestFixture.SeedCredentialsAsync(
                contextFactory,
                userId,
                "existingemail",
                passwordHash: hasher.HashPassword("Password123!"),
                emailCiphertext: emailProtection.EncryptEmail("exists@example.com"),
                emailLookupHash: emailProtection.ComputeLookupHash("exists@example.com")
            );

            var handler = provider.GetRequiredService<UserRegisterQueryHandler>();
            var result = await handler.RunQueryAsync(
                new UserRegisterQuery
                {
                    Username = "newusername",
                    Email = "exists@example.com",
                    Password = "Password123!",
                },
                CancellationToken.None
            );

            Assert.True(result.IsNone);
        }
    }

    [Fact]
    public async Task RunQueryAsync_WhenUsernameIsReserved_ReturnsNone()
    {
        var (provider, connection) = await UsersTestFixture.CreateAsync();
        await using (provider)
        await using (connection)
        {
            var handler = provider.GetRequiredService<UserRegisterQueryHandler>();
            var result = await handler.RunQueryAsync(
                new UserRegisterQuery
                {
                    Username = "settings",
                    Email = "settings@example.com",
                    Password = "Password123!",
                },
                CancellationToken.None
            );

            Assert.True(result.IsNone);
        }
    }
}
