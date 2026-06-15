using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace OpenGitBase.Common.Tests.Testing;

public static class TestDbContextServiceCollectionExtensions
{
    public static IServiceCollection AddTestDbContextFactory<TDbContext>(
        this IServiceCollection services,
        SqliteConnection connection
    )
        where TDbContext : DbContext
    {
        services.AddDbContextFactory<TDbContext>(options =>
            options.UseSqlite(connection).EnableServiceProviderCaching(false)
        );
        return services;
    }
}
