using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.Organization.Entities;
using OpenGitBase.Features.Repository;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.QueryHandlers;
using OpenGitBase.Features.Repository.Tests.Testing;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Repository.Tests.QueryHandlers;

public class GetRepositoryUsageQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenRepositoryExists_ReturnsUsage()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, RepositoryMapsterConfig>(
            typeof(GetRepositoryUsageQueryHandler).Assembly,
            typeof(UserEntity).Assembly
        );
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        var (repositoryId, _) = await RepositoryTestData.SeedPublicRepositoryAsync(seedContext);

        var quotaOptions = new RepositoryStorageQuotaOptions
        {
            MaxBytes = 1_073_741_824,
            MaxFileBytes = 52_428_800,
        };
        var contextFactory = scope.GetService<IDbContextFactory<OpenGitBaseDbContext>>();
        var handler = new GetRepositoryUsageQueryHandler(contextFactory, quotaOptions);

        var result = await handler.RunQueryAsync(
            new GetRepositoryUsageQuery { RepositoryId = repositoryId },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertSome(
            result,
            usage =>
            {
                Assert.Equal(1024, usage.BytesUsed);
                Assert.Equal(quotaOptions.MaxBytes, usage.BytesLimit);
                Assert.Equal(quotaOptions.MaxFileBytes, usage.FileSizeLimit);
            }
        );
    }

    [Fact]
    public async Task RunQueryAsync_WhenRepositoryMissing_ReturnsNone()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, RepositoryMapsterConfig>(
            typeof(GetRepositoryUsageQueryHandler).Assembly,
            typeof(UserEntity).Assembly
        );
        await scope.EnsureCreatedAsync();

        var contextFactory = scope.GetService<IDbContextFactory<OpenGitBaseDbContext>>();
        var handler = new GetRepositoryUsageQueryHandler(
            contextFactory,
            new RepositoryStorageQuotaOptions()
        );

        var result = await handler.RunQueryAsync(
            new GetRepositoryUsageQuery { RepositoryId = RepositoryId.From(Guid.NewGuid()) },
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertNone(result);
    }
}
