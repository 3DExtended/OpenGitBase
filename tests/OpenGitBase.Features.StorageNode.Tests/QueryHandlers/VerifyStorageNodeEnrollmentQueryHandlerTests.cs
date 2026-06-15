using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.DependencyInjection;
using OpenGitBase.Features.StorageNode;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.StorageNode.Entities;
using OpenGitBase.Features.StorageNode.QueryHandlers;

namespace OpenGitBase.Features.StorageNode.Tests.QueryHandlers;

public class VerifyStorageNodeEnrollmentQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_ValidToken_ReturnsEnrollmentId()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var hasher = new PasswordHasherService();
        const string token = "enrollment-token";

        var services = new ServiceCollection();
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider([typeof(StorageNodeMapsterConfig).Assembly])
        );
        services.AddTestDbContextFactory<OpenGitBaseDbContext>(connection);
        services.AddSingleton<IPasswordHasherService>(hasher);
        services.AddCqrs(options =>
            options.WithQueryHandlersFrom(typeof(VerifyStorageNodeEnrollmentQueryHandler).Assembly)
        );

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        Guid enrollmentId;
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
            enrollmentId = Guid.NewGuid();
            context
                .Set<StorageNodeEnrollmentEntity>()
                .Add(
                    new StorageNodeEnrollmentEntity
                    {
                        Id = enrollmentId,
                        NodeId = "storage-1",
                        EnrollmentTokenHash = hasher.HashPassword(token),
                        CreatedByUserId = Guid.NewGuid(),
                        CreatedAt = DateTimeOffset.UtcNow,
                        ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
                    }
                );
            await context.SaveChangesAsync();
        }

        var handler = scope.ServiceProvider.GetRequiredService<VerifyStorageNodeEnrollmentQueryHandler>();
        var result = await handler.RunQueryAsync(
            new VerifyStorageNodeEnrollmentQuery
            {
                NodeId = "storage-1",
                EnrollmentToken = token,
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Equal(enrollmentId, result.Get().Value);
    }
}
