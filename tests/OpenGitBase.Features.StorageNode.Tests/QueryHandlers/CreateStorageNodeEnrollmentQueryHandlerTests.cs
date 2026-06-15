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
}
