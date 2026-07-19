using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using OpenGitBase.Api.Controllers;
using OpenGitBase.Api.Models;
using OpenGitBase.Common.Auth;
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

namespace OpenGitBase.Api.Tests.Controllers;

public class AdminStatusControllerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(nameof(AdminStatusController.GetWindows))]
    [InlineData(nameof(AdminStatusController.Suppress))]
    [InlineData(nameof(AdminStatusController.Unsuppress))]
    [InlineData(nameof(AdminStatusController.SetWindowAnnotation))]
    public void WindowEndpoints_RequireAdminRole(string methodName)
    {
        var method = typeof(AdminStatusController).GetMethod(methodName);
        Assert.NotNull(method);
        Assert.Null(method!.GetCustomAttribute<AllowAnonymousAttribute>());

        var classAuthorize = typeof(AdminStatusController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(classAuthorize);
        Assert.Equal("admin", classAuthorize!.Roles);
    }

    [Fact]
    public async Task GetWindows_ReturnsAllWindows_IncludingSuppressed()
    {
        var (controller, provider) = await CreateControllerAsync();
        await using (provider)
        {
            await SeedAsync(provider, OpenWindow("Website", StatusComponentGroup.Website, suppressed: true));

            var result = await controller.GetWindows(CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var windows = Assert.IsType<List<AdminStatusOutageWindowDto>>(ok.Value);
            var window = Assert.Single(windows);
            Assert.True(window.Suppressed);
        }
    }

    [Fact]
    public async Task Suppress_UnknownWindow_ReturnsNotFound()
    {
        var (controller, provider) = await CreateControllerAsync();
        await using (provider)
        {
            var result = await controller.Suppress(Guid.NewGuid(), CancellationToken.None);
            Assert.IsType<NotFoundResult>(result.Result);
        }
    }

    [Fact]
    public async Task Suppress_HidesWindowFromPublicList()
    {
        var (controller, provider) = await CreateControllerAsync();
        await using (provider)
        {
            var window = OpenWindow("Git", StatusComponentGroup.Git);
            await SeedAsync(provider, window);

            var suppressResult = await controller.Suppress(window.Id, CancellationToken.None);
            var ok = Assert.IsType<OkObjectResult>(suppressResult.Result);
            var dto = Assert.IsType<AdminStatusOutageWindowDto>(ok.Value);
            Assert.True(dto.Suppressed);

            var outageWindowService = provider.GetRequiredService<StatusOutageWindowService>();
            var publicWindows = await outageWindowService.ListOpenPublicWindowsAsync(
                CancellationToken.None
            );
            Assert.Empty(publicWindows);
        }
    }

    [Fact]
    public async Task Unsuppress_MakesWindowVisibleAgain()
    {
        var (controller, provider) = await CreateControllerAsync();
        await using (provider)
        {
            var window = OpenWindow("Git", StatusComponentGroup.Git, suppressed: true);
            await SeedAsync(provider, window);

            var result = await controller.Unsuppress(window.Id, CancellationToken.None);
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<AdminStatusOutageWindowDto>(ok.Value);
            Assert.False(dto.Suppressed);

            var outageWindowService = provider.GetRequiredService<StatusOutageWindowService>();
            var publicWindows = await outageWindowService.ListOpenPublicWindowsAsync(
                CancellationToken.None
            );
            Assert.Single(publicWindows);
        }
    }

    [Fact]
    public async Task SetWindowAnnotation_UnknownWindow_ReturnsNotFound()
    {
        var (controller, provider) = await CreateControllerAsync();
        await using (provider)
        {
            var result = await controller.SetWindowAnnotation(
                Guid.NewGuid(),
                new SetStatusOutageWindowAnnotationRequest { Annotation = "Planned maintenance" },
                CancellationToken.None
            );
            Assert.IsType<NotFoundResult>(result.Result);
        }
    }

    [Fact]
    public async Task SetWindowAnnotation_SetsAndClearsAnnotation_TimesUnchanged()
    {
        var (controller, provider) = await CreateControllerAsync();
        await using (provider)
        {
            var window = OpenWindow("Git", StatusComponentGroup.Git);
            await SeedAsync(provider, window);

            var setResult = await controller.SetWindowAnnotation(
                window.Id,
                new SetStatusOutageWindowAnnotationRequest { Annotation = "Planned maintenance" },
                CancellationToken.None
            );
            var setOk = Assert.IsType<OkObjectResult>(setResult.Result);
            var setDto = Assert.IsType<AdminStatusOutageWindowDto>(setOk.Value);
            Assert.Equal("Planned maintenance", setDto.Annotation);
            Assert.Equal(window.UnhealthySince, setDto.StartedAt);
            Assert.Null(setDto.EndedAt);

            var clearResult = await controller.SetWindowAnnotation(
                window.Id,
                new SetStatusOutageWindowAnnotationRequest { Annotation = null },
                CancellationToken.None
            );
            var clearOk = Assert.IsType<OkObjectResult>(clearResult.Result);
            var clearDto = Assert.IsType<AdminStatusOutageWindowDto>(clearOk.Value);
            Assert.Null(clearDto.Annotation);
        }
    }

    private static StatusOutageWindowEntity OpenWindow(
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

    private static async Task SeedAsync(ServiceProvider provider, StatusOutageWindowEntity entity)
    {
        var contextFactory = provider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        await using var context = await contextFactory.CreateDbContextAsync();
        context.Set<StatusOutageWindowEntity>().Add(entity);
        await context.SaveChangesAsync();
    }

    private static async Task<(AdminStatusController Controller, ServiceProvider Provider)> CreateControllerAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider([typeof(StatusMapsterConfig).Assembly])
        );
        services.AddLogging();
        services.AddSingleton<ISystemClock>(new FixedSystemClock(Now));
        services.AddTestDbContextFactory<OpenGitBaseDbContext>(connection);
        services.AddScoped<StatusOutageWindowService>();
        services.AddCqrs(cfg =>
            cfg.WithQueryHandlersFrom(typeof(ListAdminStatusOutageWindowsQueryHandler).Assembly)
        );

        var provider = services.BuildServiceProvider();
        var contextFactory = provider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
        }

        var queryProcessor = provider.GetRequiredService<IQueryProcessor>();
        var userContext = Substitute.For<IUserContext>();
        var controller = new AdminStatusController(queryProcessor, userContext);
        return (controller, provider);
    }

    private sealed class FixedSystemClock(DateTimeOffset utcNow) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
