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

    [Fact]
    public async Task RunQueryAsync_WhenDatabaseFactoryThrows_ReturnsUnhealthyDatabaseResult()
    {
        var contextFactory = Substitute.For<IDbContextFactory<OpenGitBaseDbContext>>();
        contextFactory
            .CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns<Task<OpenGitBaseDbContext>>(_ => throw new InvalidOperationException("db down"));

        var services = new ServiceCollection();
        services.AddSingleton(contextFactory);
        await using var serviceProvider = services.BuildServiceProvider();

        var handler = new SystemHealthCheckQueryHandler(
            serviceProvider,
            Substitute.For<ILogger<SystemHealthCheckQueryHandler>>()
        );
        var result = await handler.RunQueryAsync(
            new SystemHealthCheckQuery { RunInParallel = false, TimeoutMs = 1000 },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        var databaseResult = result.Get().Results.Single(r => r.Name == "DatabaseConnectivity");
        Assert.Equal(HealthStatus.Unhealthy, databaseResult.Status);
    }

    [Fact]
    public async Task RunQueryAsync_WhenCancelledDuringDatabaseCheck_ReturnsUnhealthyDatabaseResult()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var services = new ServiceCollection();
        services.AddSingleton<IFeatureAssemblyProvider>(new FeatureAssemblyProvider([]));
        services.AddDbContextFactory<OpenGitBaseDbContext>(options =>
            options
                .UseSqlite($"Data Source={databaseName};Mode=Memory;Cache=Shared")
                .EnableServiceProviderCaching(false)
        );

        await using var serviceProvider = services.BuildServiceProvider();
        var contextFactory = serviceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
        }

        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var handler = new SystemHealthCheckQueryHandler(
            serviceProvider,
            Substitute.For<ILogger<SystemHealthCheckQueryHandler>>()
        );
        var result = await handler.RunQueryAsync(
            new SystemHealthCheckQuery { RunInParallel = false, TimeoutMs = 5000 },
            cancellationTokenSource.Token
        );

        Assert.True(result.IsSome);
        var databaseResult = result.Get().Results.Single(r => r.Name == "DatabaseConnectivity");
        Assert.Equal(HealthStatus.Unhealthy, databaseResult.Status);
    }

    [Fact]
    public async Task RunQueryAsync_WhenRunSequentially_ReturnsHealthyReport()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var services = new ServiceCollection();
        services.AddSingleton<IFeatureAssemblyProvider>(new FeatureAssemblyProvider([]));
        services.AddDbContextFactory<OpenGitBaseDbContext>(options =>
            options
                .UseSqlite($"Data Source={databaseName};Mode=Memory;Cache=Shared")
                .EnableServiceProviderCaching(false)
        );
        services.AddLogging();
        services.AddCqrs(options =>
            options.WithQueryHandlersFrom(typeof(SystemHealthCheckQueryHandler).Assembly)
        );

        await using var serviceProvider = services.BuildServiceProvider();
        var contextFactory = serviceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
        }

        var handler = serviceProvider.GetRequiredService<SystemHealthCheckQueryHandler>();
        var result = await handler.RunQueryAsync(
            new SystemHealthCheckQuery { RunInParallel = false, TimeoutMs = 5000 },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Equal(HealthStatus.Healthy, result.Get().Status);
    }
}
