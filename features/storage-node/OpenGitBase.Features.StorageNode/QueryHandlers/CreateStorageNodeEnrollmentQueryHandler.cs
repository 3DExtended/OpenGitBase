using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.StorageNode.Entities;

namespace OpenGitBase.Features.StorageNode.QueryHandlers;

public sealed class CreateStorageNodeEnrollmentQueryHandler
    : IQueryHandler<CreateStorageNodeEnrollmentQuery, CreateStorageNodeEnrollmentResult>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IPasswordHasherService _passwordHasherService;

    public CreateStorageNodeEnrollmentQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IPasswordHasherService passwordHasherService
    )
    {
        _contextFactory = contextFactory;
        _passwordHasherService = passwordHasherService;
    }

    public async Task<Option<CreateStorageNodeEnrollmentResult>> RunQueryAsync(
        CreateStorageNodeEnrollmentQuery query,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(query.NodeId) || query.CreatedByUserId == Guid.Empty)
        {
            return Option<CreateStorageNodeEnrollmentResult>.None;
        }

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddHours(Math.Clamp(query.ExpiresInHours, 1, 24 * 365));

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = new StorageNodeEnrollmentEntity
        {
            Id = Guid.NewGuid(),
            NodeId = query.NodeId.Trim(),
            EnrollmentTokenHash = _passwordHasherService.HashPassword(token),
            CreatedByUserId = query.CreatedByUserId,
            CreatedAt = now,
            ExpiresAt = expiresAt,
        };
        context.Set<StorageNodeEnrollmentEntity>().Add(entity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Option.From(
            new CreateStorageNodeEnrollmentResult
            {
                EnrollmentId = entity.Id,
                NodeId = entity.NodeId,
                EnrollmentToken = token,
                ExpiresAt = expiresAt,
            }
        );
    }
}
