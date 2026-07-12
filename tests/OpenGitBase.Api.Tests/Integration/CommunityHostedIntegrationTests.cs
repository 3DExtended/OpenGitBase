using Microsoft.EntityFrameworkCore;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.ComputeNode.Contracts;
using OpenGitBase.Features.ComputeNode.Entities;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Api.Tests.Integration;

public class CommunityHostedIntegrationTests
{
    [Fact]
    public async Task CrossOrgNode_ClaimsCommunityHostedJob_FromOtherOrgRepo()
    {
        await using var scope = await OrgComputeTestScope.CreateAsync();
        var orgAId = Guid.NewGuid();
        var orgBId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var repositoryId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        var enrollmentResult = await scope.CreateEnrollmentHandler.RunQueryAsync(
            new CreateComputeNodeEnrollmentQuery
            {
                NodeId = "community-node-a",
                CreatedByUserId = ownerUserId,
                OrganizationId = orgAId,
                HostingScope = ComputeHostingScope.CrossOrgAllowed,
                MaxConcurrentJobs = 2,
                MaxCpu = 2,
                MaxMemoryBytes = 2L * 1024 * 1024 * 1024,
            },
            CancellationToken.None
        );
        var registerResult = await scope.RegisterHandler.RunQueryAsync(
            new RegisterComputeNodeQuery
            {
                NodeId = "community-node-a",
                EnrollmentToken = enrollmentResult.Get().EnrollmentToken,
            },
            CancellationToken.None
        );
        var communityNode = registerResult.Get();

        await using (var context = await scope.ContextFactory.CreateDbContextAsync())
        {
            context.Set<RepositoryEntity>().Add(
                new RepositoryEntity
                {
                    Id = repositoryId,
                    Name = "org-b-repo",
                    Slug = "org-b-repo",
                    OwnerUserId = orgBId,
                    PhysicalPath = "/tmp/org-b-repo.git",
                }
            );
            context.Set<PipelineRunEntity>().Add(
                new PipelineRunEntity
                {
                    Id = runId,
                    RepositoryId = repositoryId,
                    Ref = "main",
                    AfterSha = "def456",
                    StageOrderJson = "[\"build\"]",
                    Status = PipelineRunStatus.Queued,
                    CreatedAt = DateTimeOffset.UtcNow,
                }
            );
            context.Set<PipelineJobEntity>().Add(
                new PipelineJobEntity
                {
                    Id = jobId,
                    RunId = runId,
                    Name = "build",
                    Stage = "build",
                    RunsOn = "community-hosted",
                    Status = PipelineJobStatus.Queued,
                    Script = "echo community",
                    ResolvedSpecJson = "{}",
                    EnvironmentJson = "{}",
                    CreatedAt = DateTimeOffset.UtcNow,
                }
            );
            context.Set<ComputeNodeEntity>().Add(
                new ComputeNodeEntity
                {
                    Id = Guid.NewGuid(),
                    NodeId = "platform-node",
                    OrganizationId = null,
                    HostingScope = ComputeHostingScope.CrossOrgAllowed,
                    MaxConcurrentJobs = 2,
                    MaxCpu = 2,
                    MaxMemoryBytes = 2L * 1024 * 1024 * 1024,
                    IsHealthy = true,
                    RegisteredAt = DateTimeOffset.UtcNow,
                }
            );
            context.Set<ComputeNodeEntity>().Add(
                new ComputeNodeEntity
                {
                    Id = Guid.NewGuid(),
                    NodeId = "own-org-only-node",
                    OrganizationId = orgAId,
                    HostingScope = ComputeHostingScope.OwnOrgOnly,
                    MaxConcurrentJobs = 2,
                    MaxCpu = 2,
                    MaxMemoryBytes = 2L * 1024 * 1024 * 1024,
                    IsHealthy = true,
                    RegisteredAt = DateTimeOffset.UtcNow,
                }
            );
            await context.SaveChangesAsync();
        }

        var platformNodeId = await scope.GetNodeIdAsync("platform-node");
        var ownOrgNodeId = await scope.GetNodeIdAsync("own-org-only-node");

        Assert.True(
            (await scope.ClaimHandler.RunQueryAsync(
                new ClaimPipelineJobQuery
                {
                    ComputeNodeId = platformNodeId,
                    HostingProfiles = ["community-hosted"],
                },
                CancellationToken.None
            )).IsNone
        );
        Assert.True(
            (await scope.ClaimHandler.RunQueryAsync(
                new ClaimPipelineJobQuery
                {
                    ComputeNodeId = ownOrgNodeId,
                    HostingProfiles = ["community-hosted"],
                },
                CancellationToken.None
            )).IsNone
        );

        var claim = await scope.ClaimHandler.RunQueryAsync(
            new ClaimPipelineJobQuery
            {
                ComputeNodeId = communityNode.Id.Value,
                HostingProfiles = ["community-hosted"],
            },
            CancellationToken.None
        );
        Assert.True(claim.IsSome);
        Assert.Equal(PipelineJobStatus.Running, claim.Get().Job.Status);
    }
}
