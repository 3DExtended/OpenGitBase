using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.DependencyInjection;
using OpenGitBase.Features.StorageNode;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.StorageNode.Entities;

namespace OpenGitBase.Api.Tests.Services;

public class GetFleetDispatcherSshPrivateKeyQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_ValidToken_ReturnsKeyOnce()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var hasher = new PasswordHasherService();
        var protection = new EmailProtectionService(
            new EncryptionOptions
            {
                DataKey = Convert.ToBase64String(new byte[32]),
                Pepper = "test-pepper",
            }
        );
        const string bootstrapToken = "bootstrap-token";
        const string privateKey =
            "-----BEGIN OPENSSH PRIVATE KEY-----\nabc\n-----END OPENSSH PRIVATE KEY-----";

        var services = new ServiceCollection();
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider([typeof(StorageNodeMapsterConfig).Assembly])
        );
        services.AddDbContextFactory<OpenGitBaseDbContext>(options =>
            options.UseSqlite(connection).EnableServiceProviderCaching(false)
        );
        services.AddSingleton<IPasswordHasherService>(hasher);
        services.AddSingleton<IEmailProtectionService>(protection);
        services.AddCqrs(options =>
            options.WithQueryHandlersFrom(typeof(GetFleetDispatcherSshPrivateKeyQueryHandler).Assembly)
        );

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
            context
                .Set<FleetSecretsEntity>()
                .Add(
                    new FleetSecretsEntity
                    {
                        Id = Guid.NewGuid(),
                        DispatcherSshPrivateKeyProtected = protection.EncryptEmail(privateKey),
                        FleetBootstrapTokenHash = hasher.HashPassword(bootstrapToken),
                        UpdatedAt = DateTimeOffset.UtcNow,
                    }
                );
            await context.SaveChangesAsync();
        }

        var handler = scope.ServiceProvider.GetRequiredService<GetFleetDispatcherSshPrivateKeyQueryHandler>();
        var first = await handler.RunQueryAsync(
            new GetFleetDispatcherSshPrivateKeyQuery { FleetBootstrapToken = bootstrapToken },
            CancellationToken.None
        );
        var second = await handler.RunQueryAsync(
            new GetFleetDispatcherSshPrivateKeyQuery { FleetBootstrapToken = bootstrapToken },
            CancellationToken.None
        );

        Assert.True(first.IsSome);
        Assert.Contains("openssh private key", first.Get(), StringComparison.OrdinalIgnoreCase);
        Assert.True(second.IsNone);
    }
}
