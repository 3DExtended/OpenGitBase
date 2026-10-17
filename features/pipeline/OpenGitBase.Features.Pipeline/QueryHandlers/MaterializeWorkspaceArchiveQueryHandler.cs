using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Pipeline;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;
using OpenGitBase.Features.Pipeline.Services;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Features.Pipeline.QueryHandlers;

public sealed class MaterializeWorkspaceArchiveQueryHandler
    : IQueryHandler<MaterializeWorkspaceArchiveQuery, WorkspaceArchiveResult>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IPasswordHasherService _passwordHasherService;
    private readonly IRepositoryArchiveReader _repositoryArchiveReader;

    public MaterializeWorkspaceArchiveQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IPasswordHasherService passwordHasherService,
        IRepositoryArchiveReader repositoryArchiveReader
    )
    {
        _contextFactory = contextFactory;
        _passwordHasherService = passwordHasherService;
        _repositoryArchiveReader = repositoryArchiveReader;
    }

    public async Task<Option<WorkspaceArchiveResult>> RunQueryAsync(
        MaterializeWorkspaceArchiveQuery query,
        CancellationToken cancellationToken
    )
    {
        if (
            query.JobId.Value == Guid.Empty
            || string.IsNullOrWhiteSpace(query.JobIdentityToken)
            || !JobIdentityTokens.TryParseJobId(query.JobIdentityToken, out var tokenJobId)
            || tokenJobId != query.JobId.Value
        )
        {
            return Option<WorkspaceArchiveResult>.None;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var identity = await context
            .Set<JobIdentityEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.JobId == query.JobId.Value, cancellationToken)
            .ConfigureAwait(false);
        if (
            identity is null
            || identity.RevokedAt is not null
            || identity.ExpiresAt <= DateTimeOffset.UtcNow
            || !_passwordHasherService.VerifyPassword(identity.TokenHash, query.JobIdentityToken)
        )
        {
            return Option<WorkspaceArchiveResult>.None;
        }

        var job = await context
            .Set<PipelineJobEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == query.JobId.Value, cancellationToken)
            .ConfigureAwait(false);
        if (job is null)
        {
            return Option<WorkspaceArchiveResult>.None;
        }

        var run = await context
            .Set<PipelineRunEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == job.RunId, cancellationToken)
            .ConfigureAwait(false);
        if (run is null || !GitObjectIds.IsValid(run.AfterSha))
        {
            return Option<WorkspaceArchiveResult>.None;
        }

        var repository = await context
            .Set<RepositoryEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == run.RepositoryId, cancellationToken)
            .ConfigureAwait(false);
        if (repository is null || string.IsNullOrWhiteSpace(repository.PhysicalPath))
        {
            return Option<WorkspaceArchiveResult>.None;
        }

        // The storage node builds the archive: it holds the bare repository, and the API has no git.
        // Failure throws, so the caller answers "unavailable" rather than "unauthorized".
        var archiveBytes = await _repositoryArchiveReader
            .ReadWorkspaceArchiveAsync(
                repository.Id,
                repository.PhysicalPath,
                run.AfterSha,
                job.GitDepth,
                cancellationToken
            )
            .ConfigureAwait(false);

        return Option.From(
            new WorkspaceArchiveResult
            {
                ArchiveBytes = archiveBytes,
                FileName = "workspace.tar.gz",
                IsShallow = job.GitDepth > 0,
            }
        );
    }
}
