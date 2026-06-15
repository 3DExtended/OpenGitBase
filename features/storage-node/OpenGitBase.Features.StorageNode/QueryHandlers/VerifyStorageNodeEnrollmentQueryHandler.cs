using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.StorageNode.Entities;

namespace OpenGitBase.Features.StorageNode.QueryHandlers;

public sealed class VerifyStorageNodeEnrollmentQueryHandler
    : IQueryHandler<VerifyStorageNodeEnrollmentQuery, StorageNodeEnrollmentId>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IPasswordHasherService _passwordHasherService;

    public VerifyStorageNodeEnrollmentQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IPasswordHasherService passwordHasherService
    )
    {
        _contextFactory = contextFactory;
        _passwordHasherService = passwordHasherService;
    }

    public async Task<Option<StorageNodeEnrollmentId>> RunQueryAsync(
        VerifyStorageNodeEnrollmentQuery query,
        CancellationToken cancellationToken
    )
    {
        if (
            string.IsNullOrWhiteSpace(query.NodeId)
            || string.IsNullOrWhiteSpace(query.EnrollmentToken)
        )
        {
            return Option<StorageNodeEnrollmentId>.None;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var enrollments = await context
            .Set<StorageNodeEnrollmentEntity>()
            .Where(entity => entity.NodeId == query.NodeId && entity.ConsumedAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;
        foreach (var enrollment in enrollments)
        {
            if (enrollment.ExpiresAt < now)
            {
                continue;
            }

            if (
                !_passwordHasherService.VerifyPassword(
                    enrollment.EnrollmentTokenHash,
                    query.EnrollmentToken
                )
            )
            {
                continue;
            }

            if (query.Consume)
            {
                enrollment.ConsumedAt = now;
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            return Option.From(StorageNodeEnrollmentId.From(enrollment.Id));
        }

        return Option<StorageNodeEnrollmentId>.None;
    }
}
