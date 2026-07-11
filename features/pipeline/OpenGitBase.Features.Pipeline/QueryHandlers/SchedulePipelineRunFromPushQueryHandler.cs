using System.Diagnostics;
using System.Text.Json;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;
using OpenGitBase.Features.Pipeline.Services;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Pipeline;

namespace OpenGitBase.Features.Pipeline.QueryHandlers;

public sealed class SchedulePipelineRunFromPushQueryHandler
    : IQueryHandler<SchedulePipelineRunFromPushQuery, PipelineRunId>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IMapper _mapper;
    private readonly IJobAvailableEventPublisher _jobAvailableEventPublisher;

    public SchedulePipelineRunFromPushQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IMapper mapper,
        IJobAvailableEventPublisher jobAvailableEventPublisher
    )
    {
        _contextFactory = contextFactory;
        _mapper = mapper;
        _jobAvailableEventPublisher = jobAvailableEventPublisher;
    }

    public async Task<Option<PipelineRunId>> RunQueryAsync(
        SchedulePipelineRunFromPushQuery query,
        CancellationToken cancellationToken
    )
    {
        if (query.RepositoryId == Guid.Empty || string.IsNullOrWhiteSpace(query.AfterSha))
        {
            return Option<PipelineRunId>.None;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await context
            .Set<PipelineRunEntity>()
            .FirstOrDefaultAsync(
                entity => entity.RepositoryId == query.RepositoryId && entity.AfterSha == query.AfterSha,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return Option.From(PipelineRunId.From(existing.Id));
        }

        var repository = await context
            .Set<RepositoryEntity>()
            .FirstOrDefaultAsync(entity => entity.Id == query.RepositoryId, cancellationToken)
            .ConfigureAwait(false);
        if (repository is null)
        {
            return Option<PipelineRunId>.None;
        }

        var yaml = await TryReadCiFileAsync(repository.PhysicalPath, query.AfterSha, cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return Option<PipelineRunId>.None;
        }

        var parseResult = PipelineDefinitionParser.ParsePipelineDefinition(yaml);
        if (!parseResult.IsValid || parseResult.Definition is null)
        {
            return Option<PipelineRunId>.None;
        }

        var run = new PipelineRunEntity
        {
            Id = Guid.NewGuid(),
            RepositoryId = query.RepositoryId,
            Ref = query.Ref,
            AfterSha = query.AfterSha,
            Status = PipelineRunStatus.Queued,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        context.Set<PipelineRunEntity>().Add(run);

        foreach (var job in parseResult.Definition.Jobs)
        {
            if (
                job.Only.Count > 0
                && !job.Only.Any(pattern => OnlyGlobMatcher.IsMatch(pattern, query.Ref))
            )
            {
                continue;
            }

            var jobEntity = new PipelineJobEntity
            {
                Id = Guid.NewGuid(),
                RunId = run.Id,
                Name = job.Name,
                Stage = job.Stage,
                RunsOn = job.RunsOn,
                Status = PipelineJobStatus.Queued,
                Script = job.Script,
                ResolvedSpecJson = JsonSerializer.Serialize(job),
                CreatedAt = DateTimeOffset.UtcNow,
            };
            context.Set<PipelineJobEntity>().Add(jobEntity);
            context.Set<JobStatusTransitionEntity>()
                .Add(
                    new JobStatusTransitionEntity
                    {
                        Id = Guid.NewGuid(),
                        JobId = jobEntity.Id,
                        FromStatus = PipelineJobStatus.Queued,
                        ToStatus = PipelineJobStatus.Queued,
                        Message = "Job queued by scheduler.",
                        CreatedAt = DateTimeOffset.UtcNow,
                    }
                );
            await _jobAvailableEventPublisher
                .PublishAsync(jobEntity.Id, cancellationToken)
                .ConfigureAwait(false);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var runDto = _mapper.Map<PipelineRunDto>(run);
        return Option.From(runDto.Id);
    }

    private static async Task<string?> TryReadCiFileAsync(
        string physicalPath,
        string afterSha,
        CancellationToken cancellationToken
    )
    {
        var info = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        info.ArgumentList.Add("--git-dir");
        info.ArgumentList.Add(physicalPath);
        info.ArgumentList.Add("show");
        info.ArgumentList.Add($"{afterSha}:.opengitbase-ci.yml");

        using var process = Process.Start(info);
        if (process is null)
        {
            return null;
        }

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return process.ExitCode == 0 ? output : null;
    }
}
