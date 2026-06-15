using Mapster;
using MapsterMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Common;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.DependencyInjection;
using OpenGitBase.Features.PublicGitSshKey;
using OpenGitBase.Features.PublicGitSshKey.Contracts;
using OpenGitBase.Features.PublicGitSshKey.Entities;
using OpenGitBase.Features.PublicGitSshKey.QueryHandlers;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.PublicGitSshKey.Tests.QueryHandlers;

public class GetPublicGitSshKeyByFingerprintQueryHandlerTests
{
    private const string KnownFingerprint = "SHA256:testfingerprint";
    private const string LegacyFingerprint = "testfingerprint";
    private const string PublicSshKey = "ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABgQC7";

    [Fact]
    public async Task RunQueryAsync_WhenFingerprintMissing_ReturnsNone()
    {
        await using var fixture = await CreateFixtureAsync();

        var result = await fixture.Handler.RunQueryAsync(
            new GetPublicGitSshKeyByFingerprintQuery(),
            CancellationToken.None
        );

        Assert.True(result.IsNone);
    }

    [Fact]
    public async Task RunQueryAsync_WhenFingerprintUnknown_ReturnsNone()
    {
        await using var fixture = await CreateFixtureAsync();

        var result = await fixture.Handler.RunQueryAsync(
            new GetPublicGitSshKeyByFingerprintQuery { Fingerprint = "unknown" },
            CancellationToken.None
        );

        Assert.True(result.IsNone);
    }

    [Fact]
    public async Task RunQueryAsync_WhenFingerprintKnown_ReturnsPublicGitSshKeyDto()
    {
        await using var fixture = await CreateFixtureAsync(KnownFingerprint);

        var result = await fixture.Handler.RunQueryAsync(
            new GetPublicGitSshKeyByFingerprintQuery { Fingerprint = KnownFingerprint },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Equal(PublicSshKey, result.Get().PublicSSHKey);
    }

    [Fact]
    public async Task RunQueryAsync_WhenLegacyFingerprintStored_MatchesSha256Lookup()
    {
        await using var fixture = await CreateFixtureAsync(LegacyFingerprint);

        var result = await fixture.Handler.RunQueryAsync(
            new GetPublicGitSshKeyByFingerprintQuery
            {
                Fingerprint = $"SHA256:{LegacyFingerprint}",
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Equal(PublicSshKey, result.Get().PublicSSHKey);
    }

    [Fact]
    public async Task RunQueryAsync_WhenLegacyPaddedFingerprintStored_MatchesOpenSshLookup()
    {
        const string paddedStored = "SHA256:gLmfXwUJ5fQIiHymKjrCfvoVYALr91myqq7XduS52f4=";
        const string openSshLookup = "SHA256:gLmfXwUJ5fQIiHymKjrCfvoVYALr91myqq7XduS52f4";

        await using var fixture = await CreateFixtureAsync(paddedStored);

        var result = await fixture.Handler.RunQueryAsync(
            new GetPublicGitSshKeyByFingerprintQuery { Fingerprint = openSshLookup },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Equal(PublicSshKey, result.Get().PublicSSHKey);
    }

    private static async Task<TestFixture> CreateFixtureAsync(string? storedFingerprint = null)
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
        var mapsterConfig = new TypeAdapterConfig();
        new PublicGitSshKeyMapsterConfig().Register(mapsterConfig);
        services.AddSingleton(mapsterConfig);
        services.AddSingleton<IMapper>(sp => new Mapper(
            sp.GetRequiredService<TypeAdapterConfig>()
        ));
        services.AddCqrs(options =>
            options.WithQueryHandlersFrom(
                typeof(GetPublicGitSshKeyByFingerprintQueryHandler).Assembly
            )
        );

        var serviceProvider = services.BuildServiceProvider();
        var scope = serviceProvider.CreateAsyncScope();

        var contextFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();

            if (storedFingerprint != null)
            {
                var ownerUserId = Guid.NewGuid();
                context
                    .Set<UserEntity>()
                    .Add(
                        new UserEntity
                        {
                            Id = ownerUserId,
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
                            OwnerUserId = ownerUserId,
                            Name = "Laptop",
                            PublicSSHKey = PublicSshKey,
                            Fingerprint = storedFingerprint,
                        }
                    );
                await context.SaveChangesAsync();
            }
        }

        return new TestFixture(
            scope.ServiceProvider.GetRequiredService<GetPublicGitSshKeyByFingerprintQueryHandler>(),
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
            GetPublicGitSshKeyByFingerprintQueryHandler handler,
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

        public GetPublicGitSshKeyByFingerprintQueryHandler Handler { get; }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _serviceProvider.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
