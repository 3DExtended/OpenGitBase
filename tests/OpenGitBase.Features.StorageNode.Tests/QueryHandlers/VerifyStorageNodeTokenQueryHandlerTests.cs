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

public class VerifyStorageNodeTokenQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_ValidToken_ReturnsStorageNodeId()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var hasher = new PasswordHasherService();
        var token = "secret-token";

        var services = new ServiceCollection();
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider([typeof(StorageNodeMapsterConfig).Assembly])
        );
        services.AddTestDbContextFactory<OpenGitBaseDbContext>(connection);
        services.AddSingleton<IPasswordHasherService>(hasher);
        services.AddSingleton(new StorageNodeOptions());
        var mapsterConfig = new TypeAdapterConfig();
        new StorageNodeMapsterConfig().Register(mapsterConfig);
        services.AddSingleton(mapsterConfig);
        services.AddSingleton<IMapper>(sp => new Mapper(sp.GetRequiredService<TypeAdapterConfig>()));
        services.AddCqrs(options =>
            options.WithQueryHandlersFrom(typeof(VerifyStorageNodeTokenQueryHandler).Assembly)
        );

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        Guid nodeId;
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
            var entity = StorageNodeTestData.CreateEntity();
            entity.ApiTokenHash = hasher.HashPassword(token);
            context.Set<Entities.StorageNodeEntity>().Add(entity);
            await context.SaveChangesAsync();
            nodeId = entity.Id;
        }

        var handler = scope.ServiceProvider.GetRequiredService<VerifyStorageNodeTokenQueryHandler>();
        var result = await handler.RunQueryAsync(
            new VerifyStorageNodeTokenQuery
            {
                NodeId = StorageNodeTestData.SampleNodeId,
                ApiToken = token,
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Equal(nodeId, result.Get().Value);
    }
}
