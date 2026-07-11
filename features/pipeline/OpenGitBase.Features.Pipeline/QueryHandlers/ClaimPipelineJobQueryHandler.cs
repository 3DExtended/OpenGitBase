using System.Security.Cryptography;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;

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
        var job = await context
            .Set<PipelineJobEntity>()
            .Where(entity => entity.Status == PipelineJobStatus.Queued)
            .Where(entity => query.HostingProfiles.Contains(entity.RunsOn))
            .OrderBy(entity => entity.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (job is null)
        {
            return Option<ClaimPipelineJobResultDto>.None;
        }

        job.Status = PipelineJobStatus.Running;
        job.ClaimedByComputeNodeId = query.ComputeNodeId;
        job.StartedAt = DateTimeOffset.UtcNow;

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
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
}
