using System.Reflection;
using Mapster;
using MapsterMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs.DependencyInjection;
using OpenGitBase.Features.GitAccessToken;
using OpenGitBase.Features.GitAccessToken.QueryHandlers;

namespace OpenGitBase.Features.GitAccessToken.Tests.Testing;

public sealed class GitAccessTokenHandlerTestScope : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _serviceProvider;

    public GitAccessTokenHandlerTestScope()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var handlerAssembly = typeof(CreateGitAccessTokenQueryHandler).Assembly;
        var services = new ServiceCollection();
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider([handlerAssembly, typeof(GitAccessTokenMapsterConfig).Assembly])
        );
        services.AddTestDbContextFactory<OpenGitBaseDbContext>(_connection);
        services.AddLogging();
        services.AddSingleton<IPasswordHasherService, PasswordHasherService>();
        services.AddSingleton<ISystemClock, SystemClock>();

        var mapsterConfig = new TypeAdapterConfig();
        new GitAccessTokenMapsterConfig().Register(mapsterConfig);
        services.AddSingleton(mapsterConfig);
        services.AddSingleton<IMapper>(sp => new Mapper(sp.GetRequiredService<TypeAdapterConfig>()));
        services.AddCqrs(options => options.WithQueryHandlersFrom(handlerAssembly));

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
