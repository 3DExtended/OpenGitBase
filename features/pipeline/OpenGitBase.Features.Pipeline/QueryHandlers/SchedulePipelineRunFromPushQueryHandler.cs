using System.Text.Json;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Organization.Entities;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;
using OpenGitBase.Features.Pipeline.Services;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.Users.Entities;
using OpenGitBase.Pipeline;

namespace OpenGitBase.Features.Pipeline.QueryHandlers;

public sealed class SchedulePipelineRunFromPushQueryHandler
    : IQueryHandler<SchedulePipelineRunFromPushQuery, PipelineRunId>
{
    // The compute agent reads its own control values (rootfs, overlay, layer promotion) from
    // OGB_* entries in the job environment, so pipeline files must not be able to set them.
    private const string PlatformVariablePrefix = "OGB_";

    private const string CiFilePath = ".opengitbase-ci.yml";

    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IMapper _mapper;
    private readonly IJobAvailableEventPublisher _jobAvailableEventPublisher;
    private readonly IRepositoryFileReader _repositoryFileReader;

    public SchedulePipelineRunFromPushQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IMapper mapper,
        IJobAvailableEventPublisher jobAvailableEventPublisher,
        IRepositoryFileReader repositoryFileReader
    )
    {
        _contextFactory = contextFactory;
        _mapper = mapper;
        _jobAvailableEventPublisher = jobAvailableEventPublisher;
        _repositoryFileReader = repositoryFileReader;
    }

    public async Task<Option<PipelineRunId>> RunQueryAsync(
        SchedulePipelineRunFromPushQuery query,
        CancellationToken cancellationToken
    )
    {
        if (
            query.RepositoryId == Guid.Empty
            || !GitObjectIds.IsValid(query.AfterSha)
            || GitObjectIds.IsNull(query.AfterSha)
        )
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

        var yaml = await _repositoryFileReader
            .ReadTextAsync(
                repository.Id,
                repository.PhysicalPath,
                query.AfterSha,
                CiFilePath,
                cancellationToken
            )
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

        var refName = BranchOrTagName(query.Ref);
        var scheduledJobs = parseResult.Definition.Jobs
            .Where(job => job.Only.Count == 0 || job.Only.Any(pattern => OnlyGlobMatcher.IsMatch(pattern, refName)))
            .ToList();
        if (scheduledJobs.Count == 0)
        {
            // A run only advances when one of its jobs changes, so a run without jobs would stay queued.
            return Option<PipelineRunId>.None;
        }

        var ownerContext = await ResolveOwnerContextAsync(context, repository, cancellationToken)
            .ConfigureAwait(false);

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
        var rows = new List<object> { run };

        // Later stages are released as earlier ones finish, so the first stage `only` left a job in
        // starts; a stage with nothing to run would hold the rest blocked.
        var firstStage = parseResult.Definition.Stages.First(stage =>
            scheduledJobs.Exists(job => string.Equals(job.Stage, stage, StringComparison.Ordinal))
        );
        var queuedJobIds = new List<Guid>();

        foreach (var job in scheduledJobs)
        {
            var jobId = Guid.NewGuid();
            var reservedVariables = BuildCiVariables(run, refName, repository, job, jobId, ownerContext);
            if (
                job.Variables.Keys.Any(key =>
                    reservedVariables.ContainsKey(key)
                    || key.StartsWith(PlatformVariablePrefix, StringComparison.Ordinal)
                )
            )
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
                Id = jobId,
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
            rows.Add(jobEntity);
            rows.Add(
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
                queuedJobIds.Add(jobEntity.Id);
            }
        }

        await context
            .InsertOnceAsync(
                ct =>
                {
                    context.AddRange(rows);
                    return context.SaveChangesAsync(ct);
                },
                ct => context.Set<PipelineRunEntity>().AnyAsync(entity => entity.Id == run.Id, ct),
                cancellationToken
            )
            .ConfigureAwait(false);

        // Announced only once saved, so an agent never wakes for a job it cannot claim.
        foreach (var jobId in queuedJobIds)
        {
            await _jobAvailableEventPublisher.PublishAsync(jobId, cancellationToken).ConfigureAwait(false);
        }

        var runDto = _mapper.Map<PipelineRunDto>(run);
        return Option.From(runDto.Id);
    }

    // A push reports the full ref (refs/heads/main); `only` patterns and CI_COMMIT_REF_NAME use the
    // branch or tag name, e.g. `main` or `v1.0.0`.
    private static string BranchOrTagName(string fullRef)
    {
        foreach (var prefix in (string[])["refs/heads/", "refs/tags/"])
        {
            if (fullRef.StartsWith(prefix, StringComparison.Ordinal))
            {
                return fullRef[prefix.Length..];
            }
        }

        return fullRef;
    }

    private static Dictionary<string, string> BuildCiVariables(
        PipelineRunEntity run,
        string refName,
        RepositoryEntity repository,
        ResolvedJob job,
        Guid jobId,
        RepositoryOwnerContext ownerContext
    )
    {
        var variables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CI"] = "true",
            ["CI_PIPELINE_ID"] = run.Id.ToString("D"),
            ["CI_JOB_ID"] = jobId.ToString("D"),
            ["CI_JOB_NAME"] = job.Name,
            ["CI_COMMIT_REF_NAME"] = refName,
            ["CI_COMMIT_SHA"] = run.AfterSha,
            ["CI_RUNS_ON"] = job.RunsOn,
            ["CI_PROJECT_NAME"] = repository.Name,
            ["CI_PROJECT_DIR"] = "/workspace/repo",
            ["CI_PROJECT_PATH"] = CiVariableComposer.BuildProjectPath(ownerContext.OwnerSlug, repository.Slug),
            ["CI_PROJECT_PATH_SLUG"] = CiVariableComposer.BuildProjectPathSlug(
                ownerContext.OwnerSlug,
                repository.Slug
            ),
            ["CI_JOB_EXECUTION_USER"] = job.ScriptUser,
        };

        if (ownerContext.IsOrganization)
        {
            variables["CI_ORGANIZATION_ID"] = repository.OwnerUserId.ToString("D");
        }

        return variables;
    }

    private static async Task<RepositoryOwnerContext> ResolveOwnerContextAsync(
        OpenGitBaseDbContext context,
        RepositoryEntity repository,
        CancellationToken cancellationToken
    )
    {
        var organization = await context
            .Set<OrganizationEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == repository.OwnerUserId, cancellationToken)
            .ConfigureAwait(false);
        if (organization is not null)
        {
            return new RepositoryOwnerContext(organization.Slug, IsOrganization: true);
        }

        var user = await context
            .Set<UserEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == repository.OwnerUserId, cancellationToken)
            .ConfigureAwait(false);
        return new RepositoryOwnerContext(user?.Username ?? repository.OwnerUserId.ToString("N"), false);
    }

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

    private sealed record RepositoryOwnerContext(string OwnerSlug, bool IsOrganization);
}
