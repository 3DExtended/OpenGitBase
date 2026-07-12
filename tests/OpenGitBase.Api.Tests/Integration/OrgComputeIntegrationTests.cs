using Microsoft.EntityFrameworkCore;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.ComputeNode.Contracts;
using OpenGitBase.Features.ComputeNode.Entities;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Api.Tests.Integration;

public class OrgComputeIntegrationTests
{
    [Fact]
    public async Task OrgEnrollmentToClaim_CompletesOrganizationSelfHostedJob()
    {
        await using var scope = await OrgComputeTestScope.CreateAsync();
        var orgId = Guid.NewGuid();
        var otherOrgId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var repositoryId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        var enrollmentResult = await scope.CreateEnrollmentHandler.RunQueryAsync(
            new CreateComputeNodeEnrollmentQuery
            {
                NodeId = "org-compute-1",
                CreatedByUserId = ownerUserId,
                OrganizationId = orgId,
                HostingScope = ComputeHostingScope.OwnOrgOnly,
                MaxConcurrentJobs = 2,
                MaxCpu = 2,
                MaxMemoryBytes = 2L * 1024 * 1024 * 1024,
            },
            CancellationToken.None
        );
        Assert.True(enrollmentResult.IsSome);
        var enrollmentToken = enrollmentResult.Get().EnrollmentToken;

        var registerResult = await scope.RegisterHandler.RunQueryAsync(
            new RegisterComputeNodeQuery
            {
                NodeId = "org-compute-1",
                EnrollmentToken = enrollmentToken,
            },
            CancellationToken.None
        );
        Assert.True(registerResult.IsSome);
        var orgNode = registerResult.Get();

        await using (var context = await scope.ContextFactory.CreateDbContextAsync())
        {
            context.Set<RepositoryEntity>().Add(
                new RepositoryEntity
                {
                    Id = repositoryId,
                    Name = "org-repo",
                    Slug = "org-repo",
                    OwnerUserId = orgId,
                    PhysicalPath = "/tmp/org-repo.git",
                }
            );
            context.Set<PipelineRunEntity>().Add(
                new PipelineRunEntity
                {
                    Id = runId,
                    RepositoryId = repositoryId,
                    Ref = "main",
                    AfterSha = "abc123",
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
                    RunsOn = "organization-self-hosted",
                    Status = PipelineJobStatus.Queued,
                    Script = "echo ok",
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
                    NodeId = "other-org-node",
                    OrganizationId = otherOrgId,
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
        var otherOrgNodeId = await scope.GetNodeIdAsync("other-org-node");

        var platformClaim = await scope.ClaimHandler.RunQueryAsync(
            new ClaimPipelineJobQuery
            {
                ComputeNodeId = platformNodeId,
                HostingProfiles = ["organization-self-hosted"],
            },
            CancellationToken.None
        );
        Assert.True(platformClaim.IsNone);

        var otherOrgClaim = await scope.ClaimHandler.RunQueryAsync(
            new ClaimPipelineJobQuery
            {
                ComputeNodeId = otherOrgNodeId,
                HostingProfiles = ["organization-self-hosted"],
            },
            CancellationToken.None
        );
        Assert.True(otherOrgClaim.IsNone);

        var orgClaim = await scope.ClaimHandler.RunQueryAsync(
            new ClaimPipelineJobQuery
            {
                ComputeNodeId = orgNode.Id.Value,
                HostingProfiles = ["organization-self-hosted"],
            },
            CancellationToken.None
        );
        Assert.True(orgClaim.IsSome);
        Assert.Equal(PipelineJobStatus.Running, orgClaim.Get().Job.Status);
        Assert.NotEmpty(orgClaim.Get().JobIdentityToken);

        var statusResult = await scope.UpdateStatusHandler.RunQueryAsync(
            new UpdatePipelineJobStatusQuery
            {
                JobId = PipelineJobId.From(jobId),
                Status = PipelineJobStatus.Passed,
                Message = "Completed on org node.",
                LogSection = "script",
                LogLines = ["echo ok", "exit_code=0"],
            },
            CancellationToken.None
        );
        Assert.True(statusResult.IsSome);

        await using var verifyContext = await scope.ContextFactory.CreateDbContextAsync();
        var logs = await verifyContext
            .Set<PipelineJobLogEntity>()
            .Where(entity => entity.JobId == jobId)
            .ToListAsync();
        Assert.NotEmpty(logs);
        var refreshedJob = await verifyContext
            .Set<PipelineJobEntity>()
            .FirstAsync(entity => entity.Id == jobId);
        Assert.Equal(PipelineJobStatus.Passed, refreshedJob.Status);
    }
}
