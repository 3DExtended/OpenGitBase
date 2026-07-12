using Mapster;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.DependencyInjection;
using OpenGitBase.Features.ComputeNode;
using OpenGitBase.Features.ComputeNode.Contracts;
using OpenGitBase.Features.ComputeNode.QueryHandlers;

namespace OpenGitBase.Features.ComputeNode.Tests.QueryHandlers;

public class CreateComputeNodeEnrollmentQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_CreatesEnrollmentWithToken()
    {
        await using var connection = SqliteTestConnection.OpenInMemory();

        var services = new ServiceCollection();
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider([typeof(ComputeNodeMapsterConfig).Assembly])
        );
        services.AddTestDbContextFactory<OpenGitBaseDbContext>(connection);
        services.AddSingleton<IPasswordHasherService, PasswordHasherService>();
        services.AddCqrs(options =>
            options.WithQueryHandlersFrom(typeof(CreateComputeNodeEnrollmentQueryHandler).Assembly)
        );

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
        }

        var handler = scope.ServiceProvider.GetRequiredService<CreateComputeNodeEnrollmentQueryHandler>();
        var result = await handler.RunQueryAsync(
            new CreateComputeNodeEnrollmentQuery
            {
                NodeId = "compute-1",
                CreatedByUserId = Guid.NewGuid(),
                MaxConcurrentJobs = 2,
                MaxCpu = 4,
                MaxMemoryBytes = 8_589_934_592,
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        var enrollment = result.Get();
        Assert.NotEmpty(enrollment.EnrollmentToken);
        Assert.Equal("compute-1", enrollment.NodeId);
    }
}

public class RegisterComputeNodeQueryHandlerTests;

public class ComputeNodeHeartbeatQueryHandlerTests;

public class UpdateComputeNodeCapacityQueryHandlerTests;

public class ListComputeNodesQueryHandlerTests;
