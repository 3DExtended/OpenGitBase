using Mapster;
using MapsterMapper;
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
using OpenGitBase.Features.StorageNode.QueryHandlers;

namespace OpenGitBase.Features.StorageNode.Tests.QueryHandlers;

public class CreateStorageNodeEnrollmentQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_CreatesEnrollmentWithToken()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider([typeof(StorageNodeMapsterConfig).Assembly])
        );
        services.AddTestDbContextFactory<OpenGitBaseDbContext>(connection);
        services.AddSingleton<IPasswordHasherService, PasswordHasherService>();
        services.AddCqrs(options =>
            options.WithQueryHandlersFrom(typeof(CreateStorageNodeEnrollmentQueryHandler).Assembly)
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

        var handler = scope.ServiceProvider.GetRequiredService<CreateStorageNodeEnrollmentQueryHandler>();
        var result = await handler.RunQueryAsync(
            new CreateStorageNodeEnrollmentQuery
            {
                NodeId = "storage-1",
                CreatedByUserId = Guid.NewGuid(),
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.NotEmpty(result.Get().EnrollmentToken);
    }

    [Fact]
    public async Task RunQueryAsync_StoresTheEnrollmentOnceWhenTheCommitAcknowledgementIsLost()
    {
        var lostCommit = new LostCommitSimulator();
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, StorageNodeMapsterConfig>(
            lostCommit.Configure,
            typeof(CreateStorageNodeEnrollmentQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();
        var passwordHasher = new PasswordHasherService();
        var handler = new CreateStorageNodeEnrollmentQueryHandler(
            scope.GetService<IDbContextFactory<OpenGitBaseDbContext>>(),
            passwordHasher
        );

        lostCommit.LoseNextCommitInserting("StorageNodeEnrollment");
        var result = await handler.RunQueryAsync(
            new CreateStorageNodeEnrollmentQuery { NodeId = "storage-1", CreatedByUserId = Guid.NewGuid() },
            CancellationToken.None
        );

        Assert.Equal(1, lostCommit.LostCommits);
        var created = result.Get();
        await using var context = await scope.CreateDbContextAsync();
        var enrollment = Assert.Single(await context.Set<Entities.StorageNodeEnrollmentEntity>().ToListAsync());
        Assert.Equal(created.EnrollmentId, enrollment.Id);
        Assert.True(passwordHasher.VerifyPassword(enrollment.EnrollmentTokenHash, created.EnrollmentToken));
    }
}
