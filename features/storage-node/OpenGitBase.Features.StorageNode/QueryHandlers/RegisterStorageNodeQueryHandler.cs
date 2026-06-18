using System.Security.Cryptography;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Security;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.StorageNode.Entities;

namespace OpenGitBase.Features.StorageNode.QueryHandlers;

public sealed class RegisterStorageNodeQueryHandler
    : IQueryHandler<RegisterStorageNodeQuery, RegisterStorageNodeResult>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IPasswordHasherService _passwordHasherService;
    private readonly IEmailProtectionService _emailProtectionService;
    private readonly IMapper _mapper;
    private readonly StorageNodeOptions _options;

    public RegisterStorageNodeQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IPasswordHasherService passwordHasherService,
        IEmailProtectionService emailProtectionService,
        IMapper mapper,
        StorageNodeOptions options
    )
    {
        _contextFactory = contextFactory;
        _passwordHasherService = passwordHasherService;
        _emailProtectionService = emailProtectionService;
        _mapper = mapper;
        _options = options;
    }

    public async Task<Option<RegisterStorageNodeResult>> RunQueryAsync(
        RegisterStorageNodeQuery query,
        CancellationToken cancellationToken
    )
    {
        if (
            string.IsNullOrWhiteSpace(query.NodeId)
            || string.IsNullOrWhiteSpace(query.InternalHost)
            || query.InternalHttpPort <= 0
        )
        {
            return Option<RegisterStorageNodeResult>.None;
        }

        var certificateThumbprint = NodeCertificateThumbprint.Normalize(
            query.CertificateThumbprint
        );
        if (string.IsNullOrWhiteSpace(certificateThumbprint))
        {
            return Option<RegisterStorageNodeResult>.None;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await context
            .Set<StorageNodeEntity>()
            .FirstOrDefaultAsync(node => node.NodeId == query.NodeId, cancellationToken)
            .ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;
        string? apiToken = null;

        if (existing is null)
        {
            if (string.IsNullOrWhiteSpace(query.EnrollmentToken))
            {
                return Option<RegisterStorageNodeResult>.None;
            }

            var enrollmentValid = await VerifyEnrollmentAsync(
                query.NodeId,
                query.EnrollmentToken,
                context,
                cancellationToken
            ).ConfigureAwait(false);
            if (!enrollmentValid)
            {
                return Option<RegisterStorageNodeResult>.None;
            }

            apiToken = GenerateApiToken();
            existing = new StorageNodeEntity
            {
                Id = Guid.NewGuid(),
                NodeId = query.NodeId.Trim(),
                InternalHost = query.InternalHost.Trim(),
                InternalSshPort = query.InternalSshPort,
                InternalHttpPort = query.InternalHttpPort,
                ApiTokenHash = _passwordHasherService.HashPassword(apiToken),
                ApiTokenProtected = _emailProtectionService.EncryptSecret(apiToken),
                FreeBytesAvailable = query.FreeBytesAvailable,
                TotalBytesAvailable = query.TotalBytesAvailable,
                LastHeartbeatAt = now,
                IsHealthy = true,
                RegisteredAt = now,
                CertificateThumbprint = certificateThumbprint,
            };
            context.Set<StorageNodeEntity>().Add(existing);
        }
        else
        {
            if (
                !NodeCertificateThumbprint.Matches(
                    existing.CertificateThumbprint,
                    certificateThumbprint
                )
            )
            {
                return Option<RegisterStorageNodeResult>.None;
            }

            existing.InternalHost = query.InternalHost.Trim();
            existing.InternalSshPort = query.InternalSshPort;
            existing.InternalHttpPort = query.InternalHttpPort;
            existing.FreeBytesAvailable = query.FreeBytesAvailable;
            existing.TotalBytesAvailable = query.TotalBytesAvailable;
            existing.LastHeartbeatAt = now;
            existing.IsHealthy = true;

            if (!string.IsNullOrWhiteSpace(existing.ApiTokenProtected))
            {
                try
                {
                    apiToken = _emailProtectionService.DecryptSecret(existing.ApiTokenProtected);
                }
                catch (FormatException)
                {
                    apiToken = null;
                }
                catch (System.Security.Cryptography.CryptographicException)
                {
                    apiToken = null;
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Option.From(
            new RegisterStorageNodeResult
            {
                StorageNodeId = StorageNodeId.From(existing.Id),
                ApiToken = apiToken ?? string.Empty,
                HeartbeatIntervalSeconds = _options.HeartbeatIntervalSeconds,
            }
        );
    }

    private static string GenerateApiToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private async Task<bool> VerifyEnrollmentAsync(
        string nodeId,
        string enrollmentToken,
        OpenGitBaseDbContext context,
        CancellationToken cancellationToken
    )
    {
        var enrollments = await context
            .Set<StorageNodeEnrollmentEntity>()
            .Where(entity => entity.NodeId == nodeId && entity.ConsumedAt == null)
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
                    enrollmentToken
                )
            )
            {
                continue;
            }

            enrollment.ConsumedAt = now;
            return true;
        }

        return false;
    }
}
