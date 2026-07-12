using System.Text.Json;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.ComputeNode.Contracts;
using OpenGitBase.Features.ComputeNode.Entities;
using OpenGitBase.Features.Pipeline;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Features.Pipeline.QueryHandlers;

public sealed class ClaimPipelineJobQueryHandler
    : IQueryHandler<ClaimPipelineJobQuery, ClaimPipelineJobResultDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IMapper _mapper;
    private readonly IPasswordHasherService _passwordHasherService;

    public ClaimPipelineJobQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IMapper mapper,
        IPasswordHasherService passwordHasherService
    )
    {
        _contextFactory = contextFactory;
        _mapper = mapper;
        _passwordHasherService = passwordHasherService;
    }

    public async Task<Option<ClaimPipelineJobResultDto>> RunQueryAsync(
        ClaimPipelineJobQuery query,
        CancellationToken cancellationToken
    )
    {
        if (query.ComputeNodeId == Guid.Empty || query.HostingProfiles.Count == 0)
        {
            return Option<ClaimPipelineJobResultDto>.None;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var node = await context
            .Set<ComputeNodeEntity>()
            .FirstOrDefaultAsync(entity => entity.Id == query.ComputeNodeId, cancellationToken)
            .ConfigureAwait(false);
        if (node is null || !node.IsHealthy)
        {
            return Option<ClaimPipelineJobResultDto>.None;
        }

        if (node.RunningJobs >= node.MaxConcurrentJobs)
        {
            return Option<ClaimPipelineJobResultDto>.None;
        }

        var queuedJobs = await context
            .Set<PipelineJobEntity>()
            .Where(entity => entity.Status == PipelineJobStatus.Queued || entity.Status == PipelineJobStatus.Blocked)
            .Where(entity => query.HostingProfiles.Contains(entity.RunsOn))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        queuedJobs = queuedJobs.OrderBy(entity => entity.CreatedAt).ToList();
        PipelineJobEntity? job = null;
        foreach (var candidate in queuedJobs.Where(entity => entity.Status == PipelineJobStatus.Queued))
        {
            if (!IsWithinNodeCapacity(node, candidate))
            {
                continue;
            }

            if (
                !await IsNodeEligibleForJob(node, candidate, context, cancellationToken)
                    .ConfigureAwait(false)
            )
            {
                continue;
            }

            job = candidate;
            break;
        }

        if (job is null)
        {
            return Option<ClaimPipelineJobResultDto>.None;
        }

        if (!await IsStageActiveAsync(context, job, cancellationToken).ConfigureAwait(false))
        {
            return Option<ClaimPipelineJobResultDto>.None;
        }

        job.Status = PipelineJobStatus.Running;
        job.ClaimedByComputeNodeId = query.ComputeNodeId;
        job.StartedAt = DateTimeOffset.UtcNow;
        node.RunningJobs += 1;

        var token = JobIdentityTokens.Mint(job.Id);
        context.Set<JobIdentityEntity>().Add(
            new JobIdentityEntity
            {
                Id = Guid.NewGuid(),
                JobId = job.Id,
                TokenHash = _passwordHasherService.HashPassword(token),
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            }
        );

        context.Set<JobStatusTransitionEntity>()
            .Add(
                new JobStatusTransitionEntity
                {
                    Id = Guid.NewGuid(),
                    JobId = job.Id,
                    FromStatus = PipelineJobStatus.Queued,
                    ToStatus = PipelineJobStatus.Running,
                    Message = "Job claimed.",
                    CreatedAt = DateTimeOffset.UtcNow,
                }
            );

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Option.From(
            new ClaimPipelineJobResultDto { Job = _mapper.Map<PipelineJobDto>(job), JobIdentityToken = token }
        );
    }

    private static async Task<bool> IsNodeEligibleForJob(
        ComputeNodeEntity node,
        PipelineJobEntity job,
        OpenGitBaseDbContext context,
        CancellationToken cancellationToken
    )
    {
        if (string.Equals(job.RunsOn, "ogb-hosted", StringComparison.OrdinalIgnoreCase))
        {
            return node.OrganizationId is null;
        }

        if (string.Equals(job.RunsOn, "community-hosted", StringComparison.OrdinalIgnoreCase))
        {
            return node.OrganizationId is not null
                && node.HostingScope == ComputeHostingScope.CrossOrgAllowed;
        }

        if (string.Equals(job.RunsOn, "organization-self-hosted", StringComparison.OrdinalIgnoreCase))
        {
            if (node.OrganizationId is null || node.HostingScope != ComputeHostingScope.OwnOrgOnly)
            {
                return false;
            }

            var repositoryOrgId = await ResolveRepositoryOrganizationIdAsync(context, job, cancellationToken)
                .ConfigureAwait(false);
            return repositoryOrgId.HasValue && repositoryOrgId.Value == node.OrganizationId.Value;
        }

        return false;
    }

    private static bool IsWithinNodeCapacity(ComputeNodeEntity node, PipelineJobEntity job)
    {
        var cpuAllowed = job.CpuLimit <= 0 || job.CpuLimit <= node.MaxCpu;
        var memoryAllowed =
            job.MemoryMiB <= 0 || job.MemoryMiB * 1024L * 1024 <= node.MaxMemoryBytes;
        return cpuAllowed && memoryAllowed;
    }

    private static async Task<Guid?> ResolveRepositoryOrganizationIdAsync(
        OpenGitBaseDbContext context,
        PipelineJobEntity job,
        CancellationToken cancellationToken
    )
    {
        var run = await context
            .Set<PipelineRunEntity>()
            .FirstOrDefaultAsync(entity => entity.Id == job.RunId, cancellationToken)
            .ConfigureAwait(false);
        if (run is null)
        {
            return null;
        }

        var repository = await context
            .Set<RepositoryEntity>()
            .FirstOrDefaultAsync(entity => entity.Id == run.RepositoryId, cancellationToken)
            .ConfigureAwait(false);
        return repository?.OwnerUserId;
    }

    private static async Task<bool> IsStageActiveAsync(
        OpenGitBaseDbContext context,
        PipelineJobEntity job,
        CancellationToken cancellationToken
    )
    {
        var run = await context
            .Set<PipelineRunEntity>()
            .FirstOrDefaultAsync(entity => entity.Id == job.RunId, cancellationToken)
            .ConfigureAwait(false);
        if (run is null)
        {
            return false;
        }

        var stageOrder = JsonSerializer.Deserialize<List<string>>(run.StageOrderJson) ?? [];
        var stageIndex = stageOrder.FindIndex(stage => string.Equals(stage, job.Stage, StringComparison.Ordinal));
        if (stageIndex <= 0)
        {
            return true;
        }

        var previousStages = stageOrder.Take(stageIndex).ToHashSet(StringComparer.Ordinal);
        var priorJobs = await context
            .Set<PipelineJobEntity>()
            .Where(entity => entity.RunId == run.Id && previousStages.Contains(entity.Stage))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (priorJobs.Any(entity => entity.Status is PipelineJobStatus.Failed or PipelineJobStatus.Cancelled))
        {
            return false;
        }

        return priorJobs.All(entity => entity.Status == PipelineJobStatus.Passed);
    }
}
