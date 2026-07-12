using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.ComputeNode.Contracts;
using OpenGitBase.Features.ComputeNode.Entities;

namespace OpenGitBase.Features.ComputeNode.QueryHandlers;

public sealed class CreateComputeNodeEnrollmentQueryHandler
    : IQueryHandler<CreateComputeNodeEnrollmentQuery, CreateComputeNodeEnrollmentResult>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IPasswordHasherService _passwordHasherService;

    public CreateComputeNodeEnrollmentQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IPasswordHasherService passwordHasherService
    )
    {
        _contextFactory = contextFactory;
        _passwordHasherService = passwordHasherService;
    }

    public async Task<Option<CreateComputeNodeEnrollmentResult>> RunQueryAsync(
        CreateComputeNodeEnrollmentQuery query,
        CancellationToken cancellationToken
    )
    {
        if (
            query.MaxConcurrentJobs <= 0
            || query.MaxCpu <= 0
            || query.MaxMemoryBytes <= 0
            || string.IsNullOrWhiteSpace(query.NodeId)
            || query.CreatedByUserId == Guid.Empty
        )
        {
            return Option<CreateComputeNodeEnrollmentResult>.None;
        }

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddHours(6);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = new ComputeNodeEnrollmentEntity
        {
            Id = Guid.NewGuid(),
            NodeId = query.NodeId.Trim(),
            EnrollmentTokenHash = _passwordHasherService.HashPassword(token),
            CreatedByUserId = query.CreatedByUserId,
            OrganizationId = query.OrganizationId,
            HostingScope = query.HostingScope,
            MaxConcurrentJobs = query.MaxConcurrentJobs,
            MaxCpu = query.MaxCpu,
            MaxMemoryBytes = query.MaxMemoryBytes,
            CreatedAt = now,
            ExpiresAt = expiresAt,
        };
        context.Set<ComputeNodeEnrollmentEntity>().Add(entity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Option.From(
            new CreateComputeNodeEnrollmentResult
            {
                EnrollmentId = entity.Id,
                NodeId = entity.NodeId,
                EnrollmentToken = token,
                ExpiresAt = expiresAt,
            }
        );
    }
}
