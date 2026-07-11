using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Api.Tests.Services;

public class GetRepositoryByteOverrideEligibilityQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenRepositoryExists_ReturnsEligibility()
    {
        var repositoryId = Guid.NewGuid();
        var (handler, byteOverrideService) = await CreateHandlerAsync(repositoryId);

        byteOverrideService
            .EvaluateAsync(Arg.Any<RepositoryEntity>(), Arg.Any<CancellationToken>())
            .Returns(
                new RepositoryByteOverrideEligibilityDto
                {
                    Eligible = true,
                    Reason = "Eligible for per-repository byte override.",
                    OrgContributedNodeCount = 5,
                    MaxAllowedOverride = 10_000_000_000,
                }
            );

        var result = await handler.RunQueryAsync(
            new GetRepositoryByteOverrideEligibilityQuery
            {
                RepositoryId = RepositoryId.From(repositoryId),
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.True(result.Get().Eligible);
        Assert.Equal(5, result.Get().OrgContributedNodeCount);
    }

    [Fact]
    public async Task RunQueryAsync_WhenRepositoryMissing_ReturnsNone()
    {
        var (handler, _) = await CreateHandlerAsync(Guid.NewGuid());

        var result = await handler.RunQueryAsync(
            new GetRepositoryByteOverrideEligibilityQuery
            {
                RepositoryId = RepositoryId.From(Guid.NewGuid()),
            },
            CancellationToken.None
        );

        Assert.True(result.IsNone);
    }

    private static async Task<(
        GetRepositoryByteOverrideEligibilityQueryHandler Handler,
        IRepositoryByteOverrideService ByteOverrideService
    )> CreateHandlerAsync(Guid repositoryId)
    {
        var connection = SqliteTestConnection.OpenInMemory();

        var services = new ServiceCollection();
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider([typeof(RepositoryMapsterConfig).Assembly])
        );
        services.AddTestDbContextFactory<OpenGitBaseDbContext>(connection);

        var byteOverrideService = Substitute.For<IRepositoryByteOverrideService>();
        services.AddSingleton(byteOverrideService);
        services.AddSingleton<GetRepositoryByteOverrideEligibilityQueryHandler>();

        var provider = services.BuildServiceProvider();
        var contextFactory = provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
            context
                .Set<RepositoryEntity>()
                .Add(
                    new RepositoryEntity
                    {
                        Id = repositoryId,
                        Name = "Repo",
                        Slug = "repo",
                        OwnerUserId = Guid.NewGuid(),
                        PhysicalPath = "/srv/git/repo.git",
                        Replicas = [],
                    }
                );
            await context.SaveChangesAsync();
        }

        return (
            provider.GetRequiredService<GetRepositoryByteOverrideEligibilityQueryHandler>(),
            byteOverrideService
        );
    }
}
