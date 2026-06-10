using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenGitBase.Common;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Models.HealthCheck;
using OpenGitBase.Common.Queries.HealthCheck;
using OpenGitBase.Common.QueryHandlers.HealthCheck;
using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.DependencyInjection;

namespace OpenGitBase.Common.Tests.QueryHandlers.HealthCheck;

public class SystemHealthCheckQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WithSqliteDatabase_ReturnsHealthyReport()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var services = new ServiceCollection();
        services.AddSingleton<IFeatureAssemblyProvider>(new FeatureAssemblyProvider([]));
        services.AddDbContextFactory<OpenGitBaseDbContext>(options =>
            options.UseSqlite($"Data Source={databaseName};Mode=Memory;Cache=Shared")
        );
        services.AddLogging();
        services.AddCqrs(options =>
            options.WithQueryHandlersFrom(typeof(SystemHealthCheckQueryHandler).Assembly)
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

        var handler = scope.ServiceProvider.GetRequiredService<SystemHealthCheckQueryHandler>();
        var query = new SystemHealthCheckQuery
        {
            IncludeDetails = true,
            TimeoutMs = 5000,
            RunInParallel = true,
        };

        var result = await handler.RunQueryAsync(query, CancellationToken.None);

        Assert.True(result.IsSome);
        var report = result.Get();
        Assert.NotEmpty(report.Results);
        Assert.Equal(HealthStatus.Healthy, report.Status);
        Assert.True(report.TotalDurationMs >= 0);
    }

    [Fact]
    public async Task RunQueryAsync_WithoutDatabase_SkipsDatabaseCheck()
    {
        var logger = Substitute.For<ILogger<SystemHealthCheckQueryHandler>>();
        var services = new ServiceCollection();
        services.AddSingleton(logger);
        var serviceProvider = services.BuildServiceProvider();

        var handler = new SystemHealthCheckQueryHandler(serviceProvider, logger);
        var query = new SystemHealthCheckQuery { RunInParallel = false, TimeoutMs = 1000 };

        var result = await handler.RunQueryAsync(query, CancellationToken.None);

        Assert.True(result.IsSome);
        var report = result.Get();
        Assert.Contains(
            report.Results,
            r => r.Name == "DatabaseConnectivity" && r.Status == HealthStatus.Healthy
        );
    }

    [Fact]
    public async Task RunQueryAsync_ThroughQueryProcessor_IsRegistered()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCqrs(options =>
            options.WithQueryHandlersFrom(typeof(SystemHealthCheckQueryHandler).Assembly)
        );

        await using var serviceProvider = services.BuildServiceProvider();
        var processor = serviceProvider.GetRequiredService<IQueryProcessor>();

        var result = await processor.RunQueryAsync(
            new SystemHealthCheckQuery { RunInParallel = false },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Equal(HealthStatus.Healthy, result.Get().Status);
    }
}
