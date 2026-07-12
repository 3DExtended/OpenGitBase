using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Pipeline;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;

namespace OpenGitBase.Features.Pipeline.QueryHandlers;

public sealed class ValidateJobIdentityQueryHandler
    : IQueryHandler<ValidateJobIdentityQuery, JobIdentityValidationResult>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IPasswordHasherService _passwordHasherService;

    public ValidateJobIdentityQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IPasswordHasherService passwordHasherService
    )
    {
        _contextFactory = contextFactory;
        _passwordHasherService = passwordHasherService;
    }

    public async Task<Option<JobIdentityValidationResult>> RunQueryAsync(
        ValidateJobIdentityQuery query,
        CancellationToken cancellationToken
    )
    {
        if (
            string.IsNullOrWhiteSpace(query.Token)
            || string.IsNullOrWhiteSpace(query.AfterSha)
            || !JobIdentityTokens.TryParseJobId(query.Token, out var jobId)
        )
        {
            return Option.From(
                new JobIdentityValidationResult { IsValid = false, Reason = "Missing token or SHA." }
            );
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var identity = await context
            .Set<JobIdentityEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.JobId == jobId, cancellationToken)
            .ConfigureAwait(false);
        if (
            identity is null
            || !_passwordHasherService.VerifyPassword(identity.TokenHash, query.Token)
        )
        {
            return Option.From(
                new JobIdentityValidationResult { IsValid = false, Reason = "Invalid job identity token." }
            );
        }

        if (identity.RevokedAt is not null)
        {
            return Option.From(
                new JobIdentityValidationResult
                {
                    IsValid = false,
                    Reason = "Job identity revoked.",
                    JobId = identity.JobId,
                }
            );
        }

        if (identity.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return Option.From(
                new JobIdentityValidationResult
                {
                    IsValid = false,
                    Reason = "Job identity expired.",
                    JobId = identity.JobId,
                }
            );
        }

        var job = await context
            .Set<PipelineJobEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == identity.JobId, cancellationToken)
            .ConfigureAwait(false);
        if (job is null)
        {
            return Option.From(
                new JobIdentityValidationResult { IsValid = false, Reason = "Job not found." }
            );
        }

        var run = await context
            .Set<PipelineRunEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == job.RunId, cancellationToken)
            .ConfigureAwait(false);
        if (run is null)
        {
            return Option.From(
                new JobIdentityValidationResult { IsValid = false, Reason = "Run not found." }
            );
        }

        if (run.RepositoryId != query.RepositoryId)
        {
            return Option.From(
                new JobIdentityValidationResult
                {
                    IsValid = false,
                    Reason = "Job identity scoped to a different repository.",
                    JobId = job.Id,
                }
            );
        }

        if (!string.Equals(run.AfterSha, query.AfterSha, StringComparison.OrdinalIgnoreCase))
        {
            return Option.From(
                new JobIdentityValidationResult
                {
                    IsValid = false,
                    Reason = "Job identity scoped to a different commit SHA.",
                    JobId = job.Id,
                }
            );
        }

        return Option.From(
            new JobIdentityValidationResult
            {
                IsValid = true,
                Reason = "Valid job identity.",
                JobId = job.Id,
            }
        );
    }
}
