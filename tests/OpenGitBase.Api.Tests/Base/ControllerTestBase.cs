using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenGitBase.Api.Models;
using OpenGitBase.Common.Auth;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;

namespace OpenGitBase.Api.Tests.Base;

public abstract class ControllerTestBase
    : IClassFixture<WebApplicationFactory<ApiEntryPoint>>,
        IDisposable
{
    private readonly SqliteConnection _connection;
    private bool _isDisposed;

    protected ControllerTestBase(WebApplicationFactory<ApiEntryPoint> factory)
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        Factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("E2ETest");
            builder.ConfigureServices(services =>
            {
                AuthTestServerConfiguration.ConfigureDatabase(services, _connection);
                AuthTestServerConfiguration.ConfigureOptions(services);
                AuthTestServerConfiguration.ConfigureGoogleValidator(services);
            });
        });

        Client = Factory.CreateClient();

        using var scope = Factory.Services.CreateScope();
        var serviceProvider = scope.ServiceProvider;
        QueryProcessor = serviceProvider.GetRequiredService<IQueryProcessor>();
        JwtTokenGenerator = serviceProvider.GetRequiredService<IJWTTokenGenerator>();
        PasswordHasher = serviceProvider.GetRequiredService<IPasswordHasherService>();
        ContextFactory = serviceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        Cache = serviceProvider.GetRequiredService<IMemoryCache>();

        using var initScope = Factory.Services.CreateScope();
        var contextFactory = initScope.ServiceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        using var context = contextFactory.CreateDbContext();
        context.Database.EnsureCreated();
    }

    protected HttpClient Client { get; }

    protected IMemoryCache Cache { get; }

    protected IDbContextFactory<OpenGitBaseDbContext> ContextFactory { get; }

    protected WebApplicationFactory<ApiEntryPoint> Factory { get; }

    protected IJWTTokenGenerator JwtTokenGenerator { get; }

    protected IPasswordHasherService PasswordHasher { get; }

    protected IQueryProcessor QueryProcessor { get; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected async Task<string> RegisterUserAsync(
        string username = "testuser",
        string email = "test@example.com",
        string password = "Password123!"
    )
    {
        var response = await Client.PostAsJsonAsync(
            "/register/register",
            new RegisterDto
            {
                Username = username,
                Email = email,
                Password = password,
            }
        );
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(token));
        return token;
    }

    protected async Task MarkEmailVerifiedAsync(string username)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        var credentials = await context
            .Set<OpenGitBase.Features.Users.Entities.UserCredentialsEntity>()
            .SingleAsync(x => x.Username == username);
        credentials.EmailVerified = true;
        credentials.EmailVerificationTokenHash = null;
        credentials.EmailVerificationTokenExpireDate = null;
        await context.SaveChangesAsync();
    }

    protected async Task VerifyJwtAsync(string jwt)
    {
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        var response = await Client.GetAsync("/signin/testlogin");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("ok", body);
        Client.DefaultRequestHeaders.Authorization = null;
    }

    protected void SeedRegistrationCache(string registrationToken, string internalId, string email)
    {
        SeedRegistrationCacheEntry(
            registrationToken,
            new Dictionary<string, string> { { "sub", internalId }, { "email", email } }
        );
    }

    protected void SeedRegistrationCacheEntry(
        string registrationToken,
        Dictionary<string, string> cachedRegistration
    )
    {
        Cache.Set(
            "registrationapikey" + registrationToken,
            cachedRegistration,
            TimeSpan.FromHours(2)
        );
    }

    protected (HttpClient Client, IMemoryCache Cache) CreateClientWithQueryProcessor(
        IQueryProcessor queryProcessor
    )
    {
        var customFactory = Factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("E2ETest");
            builder.ConfigureServices(services =>
            {
                AuthTestServerConfiguration.ConfigureDatabase(services, _connection);
                AuthTestServerConfiguration.ConfigureOptions(services);
                AuthTestServerConfiguration.ConfigureGoogleValidator(services);
                services.RemoveAll<IQueryProcessor>();
                services.AddSingleton(queryProcessor);
            });
        });

        using var scope = customFactory.Services.CreateScope();
        var serviceProvider = scope.ServiceProvider;
        var contextFactory = serviceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        using var context = contextFactory.CreateDbContext();
        context.Database.EnsureCreated();

        return (customFactory.CreateClient(), serviceProvider.GetRequiredService<IMemoryCache>());
    }

    protected HttpClient CreateClientWithDebugEmailVerification(bool enabled)
    {
        return Factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    AuthTestServerConfiguration.ConfigureDebugFeatures(services, enabled);
                });
            })
            .CreateClient();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }

        if (disposing)
        {
            _connection.Dispose();
        }

        _isDisposed = true;
    }
}
