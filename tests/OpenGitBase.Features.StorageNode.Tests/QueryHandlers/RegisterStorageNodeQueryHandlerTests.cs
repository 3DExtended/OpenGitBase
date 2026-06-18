using Mapster;
using MapsterMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Services;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.DependencyInjection;
using OpenGitBase.Features.StorageNode;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.StorageNode.QueryHandlers;

using OpenGitBase.Features.StorageNode.Tests.Testing;

namespace OpenGitBase.Features.StorageNode.Tests.QueryHandlers;

public class RegisterStorageNodeQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_CreatesNodeAndReturnsToken()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var services = CreateServices(connection);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var contextFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
        }

        var enrollmentHandler = scope.ServiceProvider.GetRequiredService<CreateStorageNodeEnrollmentQueryHandler>();
        var enrollment = await enrollmentHandler.RunQueryAsync(
            new CreateStorageNodeEnrollmentQuery
            {
                NodeId = "storage-1",
                CreatedByUserId = Guid.NewGuid(),
            },
            CancellationToken.None
        );
        var enrollmentToken = enrollment.Get().EnrollmentToken;

        var handler = scope.ServiceProvider.GetRequiredService<RegisterStorageNodeQueryHandler>();
        var result = await handler.RunQueryAsync(
            new RegisterStorageNodeQuery
            {
                NodeId = "storage-1",
                InternalHost = "storage-1",
                InternalHttpPort = 8081,
                FreeBytesAvailable = 5_000_000,
                TotalBytesAvailable = 10_000_000,
                EnrollmentToken = enrollmentToken,
                CertificateThumbprint = StorageNodeTestData.SampleCertificateThumbprint,
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.NotEmpty(result.Get().ApiToken);
        Assert.Equal(30, result.Get().HeartbeatIntervalSeconds);
    }

    [Fact]
    public async Task RunQueryAsync_ReRegistrationIsIdempotent()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var services = CreateServices(connection);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var contextFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
        }

        var enrollmentHandler = scope.ServiceProvider.GetRequiredService<CreateStorageNodeEnrollmentQueryHandler>();
        var enrollment = await enrollmentHandler.RunQueryAsync(
            new CreateStorageNodeEnrollmentQuery
            {
                NodeId = "storage-1",
                CreatedByUserId = Guid.NewGuid(),
            },
            CancellationToken.None
        );
        var enrollmentToken = enrollment.Get().EnrollmentToken;

        var handler = scope.ServiceProvider.GetRequiredService<RegisterStorageNodeQueryHandler>();
        var first = await handler.RunQueryAsync(
            new RegisterStorageNodeQuery
            {
                NodeId = "storage-1",
                InternalHost = "storage-1",
                InternalHttpPort = 8081,
                EnrollmentToken = enrollmentToken,
                CertificateThumbprint = StorageNodeTestData.SampleCertificateThumbprint,
            },
            CancellationToken.None
        );
        var second = await handler.RunQueryAsync(
            new RegisterStorageNodeQuery
            {
                NodeId = "storage-1",
                InternalHost = "storage-1-new",
                InternalHttpPort = 9090,
                CertificateThumbprint = StorageNodeTestData.SampleCertificateThumbprint,
            },
            CancellationToken.None
        );

        Assert.True(first.IsSome);
        Assert.True(second.IsSome);
        Assert.Equal(first.Get().StorageNodeId, second.Get().StorageNodeId);
        Assert.Equal(first.Get().ApiToken, second.Get().ApiToken);

        await using var verifyContext = await contextFactory.CreateDbContextAsync();
        var entity = await verifyContext
            .Set<Entities.StorageNodeEntity>()
            .SingleAsync(node => node.NodeId == "storage-1");
        Assert.Equal("storage-1-new", entity.InternalHost);
        Assert.Equal(9090, entity.InternalHttpPort);

        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasherService>();
        Assert.True(passwordHasher.VerifyPassword(entity.ApiTokenHash, first.Get().ApiToken));
    }

    [Fact]
    public async Task RunQueryAsync_ReRegistrationWithWrongCertificate_Fails()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var services = CreateServices(connection);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var contextFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
        }

        var enrollmentHandler = scope.ServiceProvider.GetRequiredService<CreateStorageNodeEnrollmentQueryHandler>();
        var enrollment = await enrollmentHandler.RunQueryAsync(
            new CreateStorageNodeEnrollmentQuery
            {
                NodeId = "storage-1",
                CreatedByUserId = Guid.NewGuid(),
            },
            CancellationToken.None
        );
        var enrollmentToken = enrollment.Get().EnrollmentToken;

        var handler = scope.ServiceProvider.GetRequiredService<RegisterStorageNodeQueryHandler>();
        await handler.RunQueryAsync(
            new RegisterStorageNodeQuery
            {
                NodeId = "storage-1",
                InternalHost = "storage-1",
                InternalHttpPort = 8081,
                EnrollmentToken = enrollmentToken,
                CertificateThumbprint = StorageNodeTestData.SampleCertificateThumbprint,
            },
            CancellationToken.None
        );

        var impersonationAttempt = await handler.RunQueryAsync(
            new RegisterStorageNodeQuery
            {
                NodeId = "storage-1",
                InternalHost = "evil-host",
                InternalHttpPort = 9090,
                CertificateThumbprint = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF",
            },
            CancellationToken.None
        );

        Assert.True(impersonationAttempt.IsNone);
    }

    private static ServiceCollection CreateServices(SqliteConnection connection)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider([typeof(StorageNodeMapsterConfig).Assembly])
        );
        services.AddTestDbContextFactory<OpenGitBaseDbContext>(connection);
        services.AddLogging();
        services.AddSingleton(new StorageNodeOptions());
        services.AddSingleton<IPasswordHasherService, PasswordHasherService>();
        services.AddSingleton<IEmailProtectionService>(
            new EmailProtectionService(
                new EncryptionOptions
                {
                    DataKey = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=",
                    Pepper = "test-pepper",
                }
            )
        );
        services.AddSingleton<IEmailProtectionService>(
            new EmailProtectionService(
                new EncryptionOptions
                {
                    DataKey = Convert.ToBase64String(new byte[32]),
                    Pepper = "test-pepper",
                }
            )
        );
        var mapsterConfig = new TypeAdapterConfig();
        new StorageNodeMapsterConfig().Register(mapsterConfig);
        services.AddSingleton(mapsterConfig);
        services.AddSingleton<IMapper>(sp => new Mapper(sp.GetRequiredService<TypeAdapterConfig>()));
        services.AddCqrs(options =>
            options.WithQueryHandlersFrom(typeof(RegisterStorageNodeQueryHandler).Assembly)
        );
        return services;
    }
}
