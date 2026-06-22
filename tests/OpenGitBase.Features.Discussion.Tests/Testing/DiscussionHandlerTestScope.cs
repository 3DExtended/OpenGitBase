using Mapster;
using MapsterMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using OpenGitBase.Api;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.SendGrid;
using OpenGitBase.Common.SendGrid.QueryHandlers;
using OpenGitBase.Common.Services;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.DependencyInjection;
using OpenGitBase.Features.Discussion;
using OpenGitBase.Features.Discussion.QueryHandlers;
using OpenGitBase.Features.Repository;
using OpenGitBase.Features.Users;

namespace OpenGitBase.Features.Discussion.Tests.Testing;

public sealed class DiscussionHandlerTestScope : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _serviceProvider;

    public DiscussionHandlerTestScope()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var assemblies = FeatureRegistration.GetFeatureAssemblies().ToArray();

        var services = new ServiceCollection();
        services.AddSingleton<IFeatureAssemblyProvider>(new FeatureAssemblyProvider(assemblies));
        services.AddTestDbContextFactory<OpenGitBaseDbContext>(_connection);
        services.AddLogging();
        services.AddSingleton<ISystemClock, SystemClock>();
        services.AddSingleton<IEmailProtectionService, EmailProtectionService>();
        services.AddSingleton<ISendGridEmailSender>(_ => Substitute.For<ISendGridEmailSender>());

        var mapsterConfig = new TypeAdapterConfig();
        new DiscussionMapsterConfig().Register(mapsterConfig);
        services.AddSingleton(mapsterConfig);
        services.AddSingleton<IMapper>(sp => new Mapper(sp.GetRequiredService<TypeAdapterConfig>()));
        services.AddCqrs(options =>
        {
            foreach (var assembly in assemblies)
            {
                options.WithQueryHandlersFrom(assembly);
            }
        });

        _serviceProvider = services.BuildServiceProvider();
    }

    public IQueryProcessor QueryProcessor => _serviceProvider.GetRequiredService<IQueryProcessor>();

    public async Task EnsureCreatedAsync()
    {
        await using var context = await CreateDbContextAsync();
        await context.Database.EnsureCreatedAsync();
    }

    public Task<OpenGitBaseDbContext> CreateDbContextAsync()
    {
        var factory = _serviceProvider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
        return factory.CreateDbContextAsync();
    }

    public THandler GetHandler<THandler>()
        where THandler : notnull => _serviceProvider.GetRequiredService<THandler>();

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
