using Mapster;
using MapsterMapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace OpenGitBase.Cqrs.Tests.Infrastructure;

public static class SqliteDbContextFactory
{
    public static async Task<(
        SqliteConnection Connection,
        ServiceProvider ServiceProvider
    )> CreateAsync(Action<IServiceCollection>? configure = null)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddDbContextFactory<Stubs.StubDbContext>(options => options.UseSqlite(connection));

        var mapsterConfig = new TypeAdapterConfig();
        new Stubs.StubMapsterConfig().Register(mapsterConfig);
        services.AddSingleton(mapsterConfig);
        services.AddSingleton<IMapper>(sp => new Mapper(
            sp.GetRequiredService<TypeAdapterConfig>()
        ));

        configure?.Invoke(services);

        var serviceProvider = services.BuildServiceProvider();

        var contextFactory = serviceProvider.GetRequiredService<
            IDbContextFactory<Stubs.StubDbContext>
        >();
        await using var context = await contextFactory.CreateDbContextAsync();
        await context.Database.EnsureCreatedAsync();

        return (connection, serviceProvider);
    }
}
