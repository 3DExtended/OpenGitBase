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
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Repository.Tests.QueryHandlers;

public class CreateRepositoryQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_PersistsEntity()
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
            options.WithQueryHandlersFrom(typeof(CreateRepositoryQueryHandler).Assembly)
        );

        await using var serviceProvider = services.BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();

        var contextFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        var ownerUserId = Guid.NewGuid();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
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
            await context.SaveChangesAsync();
        }

        var handler = scope.ServiceProvider.GetRequiredService<CreateRepositoryQueryHandler>();
        var result = await handler.RunQueryAsync(
            new CreateRepositoryQuery
            {
                ModelToCreate = new RepositoryDto
                {
                    Name = "Sample",
                    OwnerUserId = UserId.From(ownerUserId),
                    Slug = "sample",
                    PhysicalPath = $"./repositories/{ownerUserId}/sample",
                },
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.NotEqual(Guid.Empty, result.Get().Value);
    }
}
