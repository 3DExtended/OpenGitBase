using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs.DependencyInjection;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.QueryHandlers;

namespace OpenGitBase.Features.Repository.Tests.QueryHandlers;

public class DeleteRepositoryQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenEntityMissing_ReturnsNone()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider([typeof(DeleteRepositoryQueryHandler).Assembly])
        );
        services.AddTestDbContextFactory<OpenGitBaseDbContext>(connection);
        services.AddLogging();
        services.AddCqrs(options =>
            options.WithQueryHandlersFrom(typeof(DeleteRepositoryQueryHandler).Assembly)
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

        var handler = scope.ServiceProvider.GetRequiredService<DeleteRepositoryQueryHandler>();
        var result = await handler.RunQueryAsync(
            new DeleteRepositoryQuery { Id = RepositoryId.From(Guid.NewGuid()) },
            CancellationToken.None
        );

        Assert.True(result.IsNone);
    }
}
