using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.DependencyInjection;
using OpenGitBase.Features.Status;
using OpenGitBase.Features.Status.Contracts;
using OpenGitBase.Features.Status.Entities;
using OpenGitBase.Features.Status.QueryHandlers;
using OpenGitBase.Features.Status.Services;

namespace OpenGitBase.Features.Status.Tests.QueryHandlers;

public class GetPublicStatusWindowsQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RunQueryAsync_OmitsSuppressedWindows()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(connection);
        await using var scope = provider.CreateAsyncScope();
        await SeedAsync(
            scope,
            OpenGroupWindow("Website", StatusComponentGroup.Website, suppressed: true)
        );

        var handler = scope.ServiceProvider.GetRequiredService<GetPublicStatusWindowsQueryHandler>();
        var result = await handler.RunQueryAsync(new GetPublicStatusWindowsQuery(), CancellationToken.None);

        Assert.Empty(result.Get());
    }

    [Fact]
    public async Task RunQueryAsync_DefaultDays_ExcludesClosedWindowsOlderThanSevenDays()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(connection);
        await using var scope = provider.CreateAsyncScope();
        await SeedAsync(
            scope,
            ClosedGroupWindow("Git", StatusComponentGroup.Git, endedAt: Now.AddDays(-10))
        );

        var handler = scope.ServiceProvider.GetRequiredService<GetPublicStatusWindowsQueryHandler>();
        var result = await handler.RunQueryAsync(new GetPublicStatusWindowsQuery(), CancellationToken.None);

        Assert.Empty(result.Get());
    }

    [Fact]
    public async Task RunQueryAsync_DaysBeyondNinety_IsClampedToNinety()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(connection);
        await using var scope = provider.CreateAsyncScope();
        await SeedAsync(
            scope,
            ClosedGroupWindow("Git", StatusComponentGroup.Git, endedAt: Now.AddDays(-95))
        );

        var handler = scope.ServiceProvider.GetRequiredService<GetPublicStatusWindowsQueryHandler>();
        var result = await handler.RunQueryAsync(
            new GetPublicStatusWindowsQuery { Days = 500 },
            CancellationToken.None
        );

        Assert.Empty(result.Get());
    }

    [Fact]
    public async Task RunQueryAsync_OrdersOpenGroupThenClosedGroupThenPartial()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateProvider(connection);
        await using var scope = provider.CreateAsyncScope();

        var closed = ClosedGroupWindow("Git", StatusComponentGroup.Git, endedAt: Now.AddHours(-1));
        var open = OpenGroupWindow("Website", StatusComponentGroup.Website);
        var partial = PartialWindow("broker-1", StatusComponentGroup.MessageBus);
        await SeedAsync(scope, closed, open, partial);

        var handler = scope.ServiceProvider.GetRequiredService<GetPublicStatusWindowsQueryHandler>();
        var result = await handler.RunQueryAsync(new GetPublicStatusWindowsQuery(), CancellationToken.None);

        var windows = result.Get();
        Assert.Equal(3, windows.Count);
        Assert.Equal("Website", windows[0].DisplayName);
        Assert.True(windows[0].IsOpen);
        Assert.Equal("Git", windows[1].DisplayName);
        Assert.False(windows[1].IsOpen);
        Assert.Equal("broker-1", windows[2].DisplayName);
        Assert.True(windows[2].IsPartial);
    }

    private static StatusOutageWindowEntity OpenGroupWindow(
        string displayName,
        StatusComponentGroup group,
        bool suppressed = false
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            Scope = OutageWindowScope.Group,
            ComponentGroup = group,
            DisplayName = displayName,
            UnhealthySince = Now.AddMinutes(-10),
            BecamePublicAt = Now.AddMinutes(-5),
            EndedAt = null,
            IsPartial = false,
            Suppressed = suppressed,
            UpdatedAt = Now,
        };

    private static StatusOutageWindowEntity ClosedGroupWindow(
        string displayName,
        StatusComponentGroup group,
        DateTimeOffset endedAt
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            Scope = OutageWindowScope.Group,
            ComponentGroup = group,
            DisplayName = displayName,
            UnhealthySince = endedAt.AddHours(-1),
            BecamePublicAt = endedAt.AddMinutes(-50),
            EndedAt = endedAt,
            IsPartial = false,
            Suppressed = false,
            UpdatedAt = endedAt,
        };

    private static StatusOutageWindowEntity PartialWindow(
        string displayName,
        StatusComponentGroup group
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            Scope = OutageWindowScope.Instance,
            ComponentGroup = group,
            InstanceId = displayName,
            DisplayName = displayName,
            UnhealthySince = Now.AddMinutes(-20),
            BecamePublicAt = Now.AddMinutes(-15),
            EndedAt = null,
            IsPartial = true,
            Suppressed = false,
            UpdatedAt = Now,
        };

    private static ServiceProvider CreateProvider(SqliteConnection connection)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider([typeof(StatusMapsterConfig).Assembly])
        );
        services.AddLogging();
        services.AddSingleton<ISystemClock>(new FixedSystemClock(Now));
        services.AddTestDbContextFactory<OpenGitBaseDbContext>(connection);
        services.AddScoped<StatusOutageWindowService>();
        services.AddCqrs(cfg =>
            cfg.WithQueryHandlersFrom(typeof(GetPublicStatusWindowsQueryHandler).Assembly)
        );
        return services.BuildServiceProvider();
    }

    private static async Task SeedAsync(
        AsyncServiceScope scope,
        params StatusOutageWindowEntity[] entities
    )
    {
        var contextFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        await using var context = await contextFactory.CreateDbContextAsync();
        await context.Database.EnsureCreatedAsync();
        context.Set<StatusOutageWindowEntity>().AddRange(entities);
        await context.SaveChangesAsync();
    }

    private sealed class FixedSystemClock(DateTimeOffset utcNow) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
