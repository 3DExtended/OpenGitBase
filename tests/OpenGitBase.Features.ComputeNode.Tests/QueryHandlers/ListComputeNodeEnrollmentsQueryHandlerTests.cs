using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.ComputeNode;
using OpenGitBase.Features.ComputeNode.Contracts;
using OpenGitBase.Features.ComputeNode.Entities;
using OpenGitBase.Features.ComputeNode.QueryHandlers;

namespace OpenGitBase.Features.ComputeNode.Tests.QueryHandlers;

public class ListComputeNodeEnrollmentsQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_ReturnsPlatformEnrollments()
    {
        await using var scope = new InMemoryFeatureTestScope<OpenGitBaseDbContext, ComputeNodeMapsterConfig>(
            typeof(ListComputeNodeEnrollmentsQueryHandler).Assembly
        );
        await scope.EnsureCreatedAsync();

        await using var seedContext = await scope.CreateDbContextAsync();
        seedContext
            .Set<ComputeNodeEnrollmentEntity>()
            .Add(
                new ComputeNodeEnrollmentEntity
                {
                    Id = Guid.NewGuid(),
                    NodeId = "compute-agent-1",
                    EnrollmentTokenHash = "hash",
                    CreatedByUserId = Guid.NewGuid(),
                    MaxConcurrentJobs = 2,
                    MaxCpu = 2,
                    MaxMemoryBytes = 2L * 1024 * 1024 * 1024,
                    CreatedAt = DateTimeOffset.UtcNow,
                    ExpiresAt = DateTimeOffset.UtcNow.AddHours(6),
                }
            );
        await seedContext.SaveChangesAsync();

        var handler = scope.GetHandler<ListComputeNodeEnrollmentsQueryHandler>();
        var result = await handler.RunQueryAsync(
            new ListComputeNodeEnrollmentsQuery(),
            CancellationToken.None
        );

        QueryHandlerResultAssert.AssertSome(result, list => Assert.Single(list));
    }
}
