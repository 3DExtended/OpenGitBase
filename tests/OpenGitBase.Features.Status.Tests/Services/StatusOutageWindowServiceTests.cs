using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs.DependencyInjection;
using OpenGitBase.Features.Status;
using OpenGitBase.Features.Status.Contracts;
using OpenGitBase.Features.Status.Entities;
using OpenGitBase.Features.Status.Services;

namespace OpenGitBase.Features.Status.Tests.Services;

public class StatusOutageWindowServiceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ApplySnapshotAsync_AfterFiveMinutes_AddsOpenWindowToSnapshot()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(connection, T0);
        await using var scope = provider.CreateAsyncScope();
        await EnsureCreatedAsync(scope);

        var clock = (MutableSystemClock)scope.ServiceProvider.GetRequiredService<ISystemClock>();
        var service = scope.ServiceProvider.GetRequiredService<StatusOutageWindowService>();

        var snapshot = UnhealthyMessageBusSnapshot(T0);
        await service.ApplySnapshotAsync(snapshot, CancellationToken.None);
        Assert.Empty(snapshot.OpenWindows);

        clock.UtcNow = T0.AddMinutes(5);
        snapshot = UnhealthyMessageBusSnapshot(T0.AddMinutes(5));
        await service.ApplySnapshotAsync(snapshot, CancellationToken.None);

        Assert.Single(snapshot.OpenWindows);
        Assert.Equal("Message bus", snapshot.OpenWindows[0].DisplayName);
        Assert.True(snapshot.OpenWindows[0].IsOpen);
        Assert.Null(snapshot.OpenWindows[0].EndedAt);

        var contextFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        await using var context = await contextFactory.CreateDbContextAsync();
        var entity = await context.Set<StatusOutageWindowEntity>().SingleAsync();
        Assert.NotNull(entity.BecamePublicAt);
        Assert.Null(entity.EndedAt);
    }

    [Fact]
    public async Task PruneOlderThanAsync_ClosedWindowPastRetention_IsRemoved()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(connection, T0);
        await using var scope = provider.CreateAsyncScope();
        await EnsureCreatedAsync(scope);

        var retention = TimeSpan.FromDays(90);
        await SeedWindowAsync(
            scope,
            unhealthySince: T0 - retention - TimeSpan.FromDays(1),
            becamePublicAt: T0 - retention - TimeSpan.FromDays(1),
            endedAt: T0 - retention - TimeSpan.FromMinutes(1)
        );

        var service = scope.ServiceProvider.GetRequiredService<StatusOutageWindowService>();
        await service.PruneOlderThanAsync(retention, CancellationToken.None);

        var contextFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        await using var context = await contextFactory.CreateDbContextAsync();
        Assert.Empty(context.Set<StatusOutageWindowEntity>());
    }

    [Fact]
    public async Task PruneOlderThanAsync_ClosedWindowWithinRetention_IsKept()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(connection, T0);
        await using var scope = provider.CreateAsyncScope();
        await EnsureCreatedAsync(scope);

        var retention = TimeSpan.FromDays(90);
        await SeedWindowAsync(
            scope,
            unhealthySince: T0 - retention + TimeSpan.FromDays(1),
            becamePublicAt: T0 - retention + TimeSpan.FromDays(1),
            endedAt: T0 - retention + TimeSpan.FromMinutes(1)
        );

        var service = scope.ServiceProvider.GetRequiredService<StatusOutageWindowService>();
        await service.PruneOlderThanAsync(retention, CancellationToken.None);

        var contextFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        await using var context = await contextFactory.CreateDbContextAsync();
        Assert.Single(context.Set<StatusOutageWindowEntity>());
    }

    [Fact]
    public async Task PruneOlderThanAsync_OpenPublicWindow_IsNeverPrunedRegardlessOfAge()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(connection, T0);
        await using var scope = provider.CreateAsyncScope();
        await EnsureCreatedAsync(scope);

        var retention = TimeSpan.FromDays(90);
        await SeedWindowAsync(
            scope,
            unhealthySince: T0 - retention - TimeSpan.FromDays(365),
            becamePublicAt: T0 - retention - TimeSpan.FromDays(365),
            endedAt: null
        );

        var service = scope.ServiceProvider.GetRequiredService<StatusOutageWindowService>();
        await service.PruneOlderThanAsync(retention, CancellationToken.None);

        var contextFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        await using var context = await contextFactory.CreateDbContextAsync();
        Assert.Single(context.Set<StatusOutageWindowEntity>());
    }

    private static async Task SeedWindowAsync(
        AsyncServiceScope scope,
        DateTimeOffset unhealthySince,
        DateTimeOffset? becamePublicAt,
        DateTimeOffset? endedAt
    )
    {
        var contextFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        await using var context = await contextFactory.CreateDbContextAsync();
        context.Set<StatusOutageWindowEntity>().Add(
            new StatusOutageWindowEntity
            {
                Id = Guid.NewGuid(),
                Scope = OutageWindowScope.Group,
                ComponentGroup = StatusComponentGroup.MessageBus,
                DisplayName = "Message bus",
                UnhealthySince = unhealthySince,
                BecamePublicAt = becamePublicAt,
                EndedAt = endedAt,
                UpdatedAt = unhealthySince,
            }
        );
        await context.SaveChangesAsync();
    }

    private static PublicStatusSnapshotDto UnhealthyMessageBusSnapshot(DateTimeOffset checkedAt) =>
        new()
        {
            OverallStatus = PublicHealthStatus.Unhealthy,
            CheckedAt = checkedAt,
            Groups =
            [
                new StatusGroupSnapshot
                {
                    Group = StatusComponentGroup.MessageBus,
                    Status = PublicHealthStatus.Unhealthy,
                    Instances =
                    [
                        new StatusInstanceSnapshot
                        {
                            InstanceId = "broker-1",
                            Status = PublicHealthStatus.Unhealthy,
                            LastCheckedAt = checkedAt,
                        },
                    ],
                },
            ],
        };

    private static ServiceProvider CreateProvider(SqliteConnection connection, DateTimeOffset now)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider([typeof(StatusMapsterConfig).Assembly])
        );
        services.AddLogging();
        services.AddSingleton<ISystemClock>(new MutableSystemClock(now));
        services.AddTestDbContextFactory<OpenGitBaseDbContext>(connection);
        services.AddScoped<StatusOutageWindowService>();
        return services.BuildServiceProvider();
    }

    private static async Task EnsureCreatedAsync(AsyncServiceScope scope)
    {
        var contextFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        await using var context = await contextFactory.CreateDbContextAsync();
        await context.Database.EnsureCreatedAsync();
    }

    private sealed class MutableSystemClock(DateTimeOffset utcNow) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
    }
}
