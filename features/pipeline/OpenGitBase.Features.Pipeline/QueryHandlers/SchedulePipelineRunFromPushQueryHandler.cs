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
            StageOrderJson = JsonSerializer.Serialize(parseResult.Definition.Stages),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        context.Set<PipelineRunEntity>().Add(run);

        var firstStage = parseResult.Definition.Stages.FirstOrDefault();

        foreach (var job in parseResult.Definition.Jobs)
        {
            if (
                job.Only.Count > 0
                && !job.Only.Any(pattern => OnlyGlobMatcher.IsMatch(pattern, query.Ref))
            )
            {
                continue;
            }

            var reservedVariables = BuildCiVariables(run, repository, job);
            if (job.Variables.Keys.Any(key => reservedVariables.ContainsKey(key)))
            {
                return Option<PipelineRunId>.None;
            }

            var effectiveVariables = new Dictionary<string, string>(
                reservedVariables,
                StringComparer.Ordinal
            );
            foreach (var pair in job.Variables)
            {
                effectiveVariables[pair.Key] = pair.Value;
            }

            var gitDepth = ResolveGitDepth(effectiveVariables);
            var isOgbHosted = string.Equals(
                job.RunsOn,
                "ogb-hosted",
                StringComparison.OrdinalIgnoreCase
            );
            var jobEntity = new PipelineJobEntity
            {
                Id = Guid.NewGuid(),
                RunId = run.Id,
                Name = job.Name,
                Stage = job.Stage,
                RunsOn = job.RunsOn,
                Status = string.Equals(job.Stage, firstStage, StringComparison.Ordinal)
                    ? PipelineJobStatus.Queued
                    : PipelineJobStatus.Blocked,
                Script = job.Script,
                ResolvedSpecJson = JsonSerializer.Serialize(job),
                EnvironmentJson = JsonSerializer.Serialize(effectiveVariables),
                GitDepth = gitDepth,
                CpuLimit = isOgbHosted ? 1 : 0,
                MemoryMiB = isOgbHosted ? 2048 : 0,
                DiskGiB = isOgbHosted ? 20 : 0,
                TimeoutSeconds = isOgbHosted ? 30 * 60 : 0,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            context.Set<PipelineJobEntity>().Add(jobEntity);
            context.Set<JobStatusTransitionEntity>()
                .Add(
                    new JobStatusTransitionEntity
                    {
                        Id = Guid.NewGuid(),
                        JobId = jobEntity.Id,
                        FromStatus = jobEntity.Status,
                        ToStatus = jobEntity.Status,
                        Message = jobEntity.Status == PipelineJobStatus.Queued
                            ? "Job queued by scheduler."
                            : "Job blocked until prior stages complete.",
                        CreatedAt = DateTimeOffset.UtcNow,
                    }
                );
            if (jobEntity.Status == PipelineJobStatus.Queued)
            {
                await _jobAvailableEventPublisher
                    .PublishAsync(jobEntity.Id, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var runDto = _mapper.Map<PipelineRunDto>(run);
        return Option.From(runDto.Id);
    }

    private static Dictionary<string, string> BuildCiVariables(
        PipelineRunEntity run,
        RepositoryEntity repository,
        ResolvedJob job
    ) =>
        new(StringComparer.Ordinal)
        {
            ["CI"] = "true",
            ["CI_PIPELINE_ID"] = run.Id.ToString("D"),
            ["CI_JOB_NAME"] = job.Name,
            ["CI_COMMIT_REF_NAME"] = run.Ref,
            ["CI_COMMIT_SHA"] = run.AfterSha,
            ["CI_RUNS_ON"] = job.RunsOn,
            ["CI_PROJECT_NAME"] = repository.Name,
            ["CI_PROJECT_DIR"] = "/workspace/repo",
            ["CI_REPOSITORY_GIT_DIR"] = repository.PhysicalPath,
        };

    private static int ResolveGitDepth(IReadOnlyDictionary<string, string> variables)
    {
        if (
            variables.TryGetValue("GIT_DEPTH", out var value)
            && int.TryParse(value, out var parsed)
            && parsed >= 0
        )
        {
            return parsed;
        }

        return 0;
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
