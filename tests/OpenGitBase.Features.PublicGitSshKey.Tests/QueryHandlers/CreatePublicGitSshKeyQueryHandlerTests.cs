using Mapster;
using MapsterMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Common;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.DependencyInjection;
using OpenGitBase.Features.PublicGitSshKey;
using OpenGitBase.Features.PublicGitSshKey.Contracts;
using OpenGitBase.Features.PublicGitSshKey.QueryHandlers;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.PublicGitSshKey.Tests.QueryHandlers;

public class CreatePublicGitSshKeyQueryHandlerTests
{
    private const string SamplePublicKey = "ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABgQC7";

    [Fact]
    public async Task RunQueryAsync_PersistsEntity()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider([typeof(PublicGitSshKeyMapsterConfig).Assembly])
        );
        services.AddDbContextFactory<OpenGitBaseDbContext>(options =>
            options.UseSqlite(connection)
        );

        services.AddLogging();
        var mapsterConfig = new TypeAdapterConfig();
        new PublicGitSshKeyMapsterConfig().Register(mapsterConfig);
        services.AddSingleton(mapsterConfig);
        services.AddSingleton<IMapper>(sp => new Mapper(
            sp.GetRequiredService<TypeAdapterConfig>()
        ));
        services.AddCqrs(options =>
            options.WithQueryHandlersFrom(typeof(CreatePublicGitSshKeyQueryHandler).Assembly)
        );

        await using var serviceProvider = services.BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();

        var contextFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        var ownerUserId = Guid.NewGuid();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
            context.Set<UserEntity>().Add(
                new UserEntity
                {
                    Id = ownerUserId,
                    Username = "testuser",
                    NormalizedUsername = "TESTUSER",
                    CreatedAt = DateTimeOffset.UtcNow,
                }
            );
            await context.SaveChangesAsync();
        }

        var handler = scope.ServiceProvider.GetRequiredService<CreatePublicGitSshKeyQueryHandler>();
        var result = await handler.RunQueryAsync(
            new CreatePublicGitSshKeyQuery
            {
                ModelToCreate = new PublicGitSshKeyDto
                {
                    OwnerUserId = UserId.From(ownerUserId),
                    Name = "Sample",
                    PublicSSHKey = SamplePublicKey,
                    Fingerprint = "SHA256:testfingerprint",
                },
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.NotEqual(Guid.Empty, result.Get().Value);
    }
}
