using Mapster;
using MapsterMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Common;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.DependencyInjection;
using OpenGitBase.Features.PublicGitSshKey;
using OpenGitBase.Features.PublicGitSshKey.Contracts;
using OpenGitBase.Features.PublicGitSshKey.QueryHandlers;

namespace OpenGitBase.Features.PublicGitSshKey.Tests.QueryHandlers;

public class CreatePublicGitSshKeyQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_PersistsEntity()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
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
            options.WithQueryHandlersFrom(typeof(CreatePublicGitSshKeyQueryHandler).Assembly)
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

        var handler = scope.ServiceProvider.GetRequiredService<CreatePublicGitSshKeyQueryHandler>();
        var result = await handler.RunQueryAsync(
            new CreatePublicGitSshKeyQuery
            {
                ModelToCreate = new PublicGitSshKeyDto { Name = "Sample" },
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.NotEqual(Guid.Empty, result.Get().Value);
    }
}
