using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Common;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.DependencyInjection;
using OpenGitBase.Features.PublicGitSshKey;
using OpenGitBase.Features.PublicGitSshKey.Contracts;
using OpenGitBase.Features.PublicGitSshKey.Entities;
using OpenGitBase.Features.PublicGitSshKey.QueryHandlers;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.PublicGitSshKey.Tests.QueryHandlers;

public class GetUserIdBySshKeyFingerprintQueryHandlerTests
{
    private const string KnownFingerprint = "SHA256:testfingerprint";

    [Fact]
    public async Task RunQueryAsync_WhenFingerprintMissing_ReturnsNone()
    {
        await using var fixture = await CreateFixtureAsync();

        var result = await fixture.Handler.RunQueryAsync(
            new GetUserIdBySshKeyFingerprintQuery(),
            CancellationToken.None
        );

        Assert.True(result.IsNone);
    }

    [Fact]
    public async Task RunQueryAsync_WhenFingerprintUnknown_ReturnsNone()
    {
        await using var fixture = await CreateFixtureAsync();

        var result = await fixture.Handler.RunQueryAsync(
            new GetUserIdBySshKeyFingerprintQuery { Fingerprint = "unknown" },
            CancellationToken.None
        );

        Assert.True(result.IsNone);
    }

    [Fact]
    public async Task RunQueryAsync_WhenFingerprintKnown_ReturnsOwnerUserId()
    {
        var ownerUserId = Guid.NewGuid();
        await using var fixture = await CreateFixtureAsync(ownerUserId);

        var result = await fixture.Handler.RunQueryAsync(
            new GetUserIdBySshKeyFingerprintQuery { Fingerprint = KnownFingerprint },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Equal(ownerUserId, result.Get().Value);
    }

    private static async Task<TestFixture> CreateFixtureAsync(Guid? ownerUserId = null)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider([typeof(PublicGitSshKeyMapsterConfig).Assembly])
        );
        services.AddDbContextFactory<OpenGitBaseDbContext>(options =>
            options.UseSqlite(connection)
        );
        services.AddLogging();
        services.AddCqrs(options =>
            options.WithQueryHandlersFrom(typeof(GetUserIdBySshKeyFingerprintQueryHandler).Assembly)
        );

        var serviceProvider = services.BuildServiceProvider();
        var scope = serviceProvider.CreateAsyncScope();

        var contextFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();

            if (ownerUserId.HasValue)
            {
                context
                    .Set<UserEntity>()
                    .Add(
                        new UserEntity
                        {
                            Id = ownerUserId.Value,
                            Username = "testuser",
                            NormalizedUsername = "TESTUSER",
                            CreatedAt = DateTimeOffset.UtcNow,
                        }
                    );
                context
                    .Set<PublicGitSshKeyEntity>()
                    .Add(
                        new PublicGitSshKeyEntity
                        {
                            Id = Guid.NewGuid(),
                            OwnerUserId = ownerUserId.Value,
                            Name = "Laptop",
                            PublicSSHKey = "ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABgQC7",
                            Fingerprint = KnownFingerprint,
                        }
                    );
                await context.SaveChangesAsync();
            }
        }

        return new TestFixture(
            scope.ServiceProvider.GetRequiredService<GetUserIdBySshKeyFingerprintQueryHandler>(),
            connection,
            scope,
            serviceProvider
        );
    }

    private sealed class TestFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly AsyncServiceScope _scope;
        private readonly ServiceProvider _serviceProvider;

        public TestFixture(
            GetUserIdBySshKeyFingerprintQueryHandler handler,
            SqliteConnection connection,
            AsyncServiceScope scope,
            ServiceProvider serviceProvider
        )
        {
            Handler = handler;
            _connection = connection;
            _scope = scope;
            _serviceProvider = serviceProvider;
        }

        public GetUserIdBySshKeyFingerprintQueryHandler Handler { get; }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _serviceProvider.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
