using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Services;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.DependencyInjection;
using OpenGitBase.Features.StorageNode;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Tests.Services;

public class GenerateFleetDispatcherSshKeysQueryHandlerTests
{
    // OpenSSH keys are case-sensitive base64 and OpenSSH refuses a key file without the
    // trailing newline after the END marker, so the stored key must come back byte-for-byte.
    private const string MixedCasePrivateKey =
        "-----BEGIN OPENSSH PRIVATE KEY-----\n"
        + "b3BlbnNzaC1rZXktdjEAAAAABG5vbmUAAAAEbm9uZQAAAAAAAAABAAAAMwAAAAtzc2gtZW\n"
        + "QyNTUxOQAAACBXbFm3ZqT8kR2wYdN0vPcJ4uLxHs9eGaKfI1oMnQrStAAAAJiKx7Wx\n"
        + "-----END OPENSSH PRIVATE KEY-----\n";

    private const string PublicKey =
        "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIFdsWbdmpPyRHbBh03S89wni4vEez14Zop8jWgydCtK0 opengitbase-dispatcher-storage";

    [Fact]
    public async Task RunQueryAsync_StoredPrivateKey_RoundTripsByteForByteThroughBootstrapFetch()
    {
        await using var connection = SqliteTestConnection.OpenInMemory();

        var services = new ServiceCollection();
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider([typeof(StorageNodeMapsterConfig).Assembly])
        );
        services.AddDbContextFactory<OpenGitBaseDbContext>(options =>
            options.UseSqlite(connection).EnableServiceProviderCaching(false)
        );
        services.AddSingleton<IPasswordHasherService>(new PasswordHasherService());
        services.AddSingleton<IEmailProtectionService>(
            new EmailProtectionService(
                new EncryptionOptions
                {
                    DataKey = Convert.ToBase64String(new byte[32]),
                    Pepper = "test-pepper",
                }
            )
        );
        services.AddSingleton<IFleetSshKeyGenerator>(
            new FixedFleetSshKeyGenerator(PublicKey, MixedCasePrivateKey)
        );
        services.AddCqrs(options =>
            options.WithQueryHandlersFrom(typeof(GenerateFleetDispatcherSshKeysQueryHandler).Assembly)
        );

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
        }

        var generateHandler =
            scope.ServiceProvider.GetRequiredService<GenerateFleetDispatcherSshKeysQueryHandler>();
        var getHandler =
            scope.ServiceProvider.GetRequiredService<GetFleetDispatcherSshPrivateKeyQueryHandler>();

        var generated = await generateHandler.RunQueryAsync(
            new GenerateFleetDispatcherSshKeysQuery(),
            CancellationToken.None
        );
        var fetched = await getHandler.RunQueryAsync(
            new GetFleetDispatcherSshPrivateKeyQuery
            {
                FleetBootstrapToken = generated.Get().FleetBootstrapToken,
            },
            CancellationToken.None
        );

        Assert.Equal(PublicKey, generated.Get().DispatcherSshPublicKey);
        Assert.True(fetched.IsSome);
        Assert.Equal(MixedCasePrivateKey, fetched.Get());
    }

    private sealed class FixedFleetSshKeyGenerator : IFleetSshKeyGenerator
    {
        private readonly string _publicKey;
        private readonly string _privateKey;

        public FixedFleetSshKeyGenerator(string publicKey, string privateKey)
        {
            _publicKey = publicKey;
            _privateKey = privateKey;
        }

        public (string PublicKey, string PrivateKey) GenerateEd25519KeyPair() =>
            (_publicKey, _privateKey);
    }
}
