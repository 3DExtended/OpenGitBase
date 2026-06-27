using Mapster;
using MapsterMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.DependencyInjection;
using OpenGitBase.Features.Repository;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.QueryHandlers;
using OpenGitBase.Features.Repository.Tests.Testing;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Repository.Tests.QueryHandlers;

public class GetRepositoryQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenEntityMissing_ReturnsNone()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider([typeof(RepositoryMapsterConfig).Assembly])
        );
        services.AddTestDbContextFactory<OpenGitBaseDbContext>(connection);
        services.AddLogging();
        var mapsterConfig = new TypeAdapterConfig();
        new RepositoryMapsterConfig().Register(mapsterConfig);
        services.AddSingleton(mapsterConfig);
        services.AddSingleton<IMapper>(sp => new Mapper(
            sp.GetRequiredService<TypeAdapterConfig>()
        ));
        services.AddCqrs(options =>
            options.WithQueryHandlersFrom(typeof(GetRepositoryQueryHandler).Assembly)
        );

        await using var serviceProvider = services.BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();

        var contextFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
        }

        var handler = scope.ServiceProvider.GetRequiredService<GetRepositoryQueryHandler>();
        var result = await handler.RunQueryAsync(
            new GetRepositoryQuery { ModelId = RepositoryId.From(Guid.NewGuid()) },
            CancellationToken.None
        );

        Assert.True(result.IsNone);
    }

    [Fact]
    public async Task RunQueryAsync_ThroughQueryProcessor_EnrichesOwnerSlug()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, RepositoryMapsterConfig>(
            typeof(GetRepositoryQueryHandler).Assembly,
            typeof(UserEntity).Assembly
        );
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var (repositoryId, _) = await RepositoryTestData.SeedPublicRepositoryAsync(
            seedContext,
            "demo-user",
            "hello-world"
        );

        var processor = scope.GetService<IQueryProcessor>();
        var result = await processor.RunQueryAsync(
            new GetRepositoryQuery { ModelId = repositoryId },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertSome(
            result,
            repository =>
            {
                Assert.Equal("demo-user", repository.OwnerSlug);
                Assert.Equal("hello-world", repository.Slug);
            }
        );
    }
}
