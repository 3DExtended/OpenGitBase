using System.Reflection;
using Mapster;
using MapsterMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs.DependencyInjection;

namespace OpenGitBase.Common.Tests.Testing;

public sealed class InMemoryFeatureTestScope<TDbContext, TMapsterConfig> : IAsyncDisposable
    where TDbContext : DbContext
    where TMapsterConfig : IRegister, new()
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _serviceProvider;

    public InMemoryFeatureTestScope(
        Assembly handlerAssembly,
        params Assembly[] additionalConfigurationAssemblies
    )
    {
        _connection = SqliteTestConnection.OpenInMemory();

        var assemblies = new List<Assembly> { handlerAssembly };
        assemblies.AddRange(additionalConfigurationAssemblies);
        assemblies.Add(typeof(TMapsterConfig).Assembly);

        var services = new ServiceCollection();
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider(assemblies.Distinct().ToArray())
        );
        services.AddTestDbContextFactory<TDbContext>(_connection);
        services.AddLogging();

        var mapsterConfig = new TypeAdapterConfig();
        new TMapsterConfig().Register(mapsterConfig);
        services.AddSingleton(mapsterConfig);
        services.AddSingleton<IMapper>(sp => new Mapper(
            sp.GetRequiredService<TypeAdapterConfig>()
        ));
        services.AddCqrs(options => options.WithQueryHandlersFrom(handlerAssembly));

        _serviceProvider = services.BuildServiceProvider();
    }

    public async Task EnsureCreatedAsync()
    {
        await using var context = await CreateDbContextAsync().ConfigureAwait(false);
        await context.Database.EnsureCreatedAsync().ConfigureAwait(false);
    }

    public Task<TDbContext> CreateDbContextAsync()
    {
        var factory = _serviceProvider.GetRequiredService<IDbContextFactory<TDbContext>>();
        return factory.CreateDbContextAsync();
    }

    public THandler GetHandler<THandler>()
        where THandler : notnull => _serviceProvider.GetRequiredService<THandler>();

    public TService GetService<TService>()
        where TService : notnull => _serviceProvider.GetRequiredService<TService>();

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync().ConfigureAwait(false);
        await _connection.DisposeAsync().ConfigureAwait(false);
    }
}
