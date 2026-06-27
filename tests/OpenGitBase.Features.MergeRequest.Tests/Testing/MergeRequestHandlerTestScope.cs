using System.Reflection;
using Mapster;
using MapsterMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.DependencyInjection;
using OpenGitBase.Features.MergeRequest;
using OpenGitBase.Features.MergeRequest.QueryHandlers;
using OpenGitBase.Features.Users;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.MergeRequest.Tests.Testing;

public sealed class MergeRequestHandlerTestScope : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _serviceProvider;

    public MergeRequestHandlerTestScope()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var assemblies = new[]
        {
            typeof(CreateMergeRequestQueryHandler).Assembly,
            typeof(UserEntity).Assembly,
            typeof(OpenGitBase.Features.Repository.RepositoryMapsterConfig).Assembly,
            typeof(OpenGitBase.Features.Discussion.DiscussionMapsterConfig).Assembly,
        };

        var services = new ServiceCollection();
        services.AddSingleton<IFeatureAssemblyProvider>(new FeatureAssemblyProvider(assemblies));
        services.AddTestDbContextFactory<OpenGitBaseDbContext>(_connection);
        services.AddLogging();
        services.AddSingleton<ISystemClock, SystemClock>();

        var mapsterConfig = new TypeAdapterConfig();
        new MergeRequestMapsterConfig().Register(mapsterConfig);
        new OpenGitBase.Features.Repository.RepositoryMapsterConfig().Register(mapsterConfig);
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
