using Mapster;
using MapsterMapper;
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
using OpenGitBase.Features.ComputeNode.Entities;
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

    [Fact]
    public async Task RunQueryAsync_StoresTheEnrollmentOnceWhenTheCommitAcknowledgementIsLost()
    {
        var lostCommit = new LostCommitSimulator();
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, ComputeNodeMapsterConfig>(
            lostCommit.Configure,
            typeof(CreateComputeNodeEnrollmentQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();
        var passwordHasher = new PasswordHasherService();
        var handler = new CreateComputeNodeEnrollmentQueryHandler(
            scope.GetService<IDbContextFactory<OpenGitBaseDbContext>>(),
            passwordHasher
        );

        lostCommit.LoseNextCommitInserting("ComputeNodeEnrollment");
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

        Assert.Equal(1, lostCommit.LostCommits);
        var created = result.Get();
        await using var context = await scope.CreateDbContextAsync();
        var enrollment = Assert.Single(await context.Set<ComputeNodeEnrollmentEntity>().ToListAsync());
        Assert.Equal(created.EnrollmentId, enrollment.Id);
        Assert.True(passwordHasher.VerifyPassword(enrollment.EnrollmentTokenHash, created.EnrollmentToken));
    }
}

public class RegisterComputeNodeQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_RegistersTheNodeOnceWhenTheCommitAcknowledgementIsLost()
    {
        // The node's identity token exists only in this response; a 500 here leaves a registered node whose agent
        // never learns it, and the consumed enrollment cannot register it again.
        var lostCommit = new LostCommitSimulator();
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, ComputeNodeMapsterConfig>(
            lostCommit.Configure,
            typeof(RegisterComputeNodeQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();
        var passwordHasher = new PasswordHasherService();
        var enrollmentId = Guid.NewGuid();
        await using (var seedContext = await scope.CreateDbContextAsync())
        {
            seedContext.Set<ComputeNodeEnrollmentEntity>().Add(
                new ComputeNodeEnrollmentEntity
                {
                    Id = enrollmentId,
                    NodeId = "compute-1",
                    EnrollmentTokenHash = passwordHasher.HashPassword("enrollment-token"),
                    CreatedByUserId = Guid.NewGuid(),
                    MaxConcurrentJobs = 2,
                    MaxCpu = 4,
                    MaxMemoryBytes = 8_589_934_592,
                    CreatedAt = DateTimeOffset.UtcNow,
                    ExpiresAt = DateTimeOffset.UtcNow.AddHours(6),
                }
            );
            await seedContext.SaveChangesAsync();
        }

        var handler = new RegisterComputeNodeQueryHandler(
            scope.GetService<IDbContextFactory<OpenGitBaseDbContext>>(),
            passwordHasher,
            scope.GetService<IMapper>()
        );
        lostCommit.LoseNextCommitInserting("ComputeNode");
        var result = await handler.RunQueryAsync(
            new RegisterComputeNodeQuery { NodeId = "compute-1", EnrollmentToken = "enrollment-token" },
            CancellationToken.None
        );

        Assert.Equal(1, lostCommit.LostCommits);
        var registered = result.Get();
        await using var context = await scope.CreateDbContextAsync();
        var node = Assert.Single(await context.Set<ComputeNodeEntity>().ToListAsync());
        Assert.Equal(node.Id, registered.Node.Id.Value);
        Assert.True(passwordHasher.VerifyPassword(node.IdentityTokenHash, registered.NodeIdentityToken));
        var enrollment = await context.Set<ComputeNodeEnrollmentEntity>().SingleAsync(x => x.Id == enrollmentId);
        Assert.NotNull(enrollment.ConsumedAt);
    }
}

public class ComputeNodeHeartbeatQueryHandlerTests;

public class UpdateComputeNodeCapacityQueryHandlerTests;

public class ListComputeNodesQueryHandlerTests;
