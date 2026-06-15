using System.Security.Cryptography;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
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

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await context
            .Set<StorageNodeEntity>()
            .FirstOrDefaultAsync(node => node.NodeId == query.NodeId, cancellationToken)
            .ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;
        string? apiToken = null;

        if (existing is null)
        {
            apiToken = GenerateApiToken();
            existing = new StorageNodeEntity
            {
                Id = Guid.NewGuid(),
                NodeId = query.NodeId.Trim(),
                InternalHost = query.InternalHost.Trim(),
                InternalSshPort = query.InternalSshPort,
                InternalHttpPort = query.InternalHttpPort,
                ApiTokenHash = _passwordHasherService.HashPassword(apiToken),
                ApiTokenProtected = _emailProtectionService.EncryptEmail(apiToken),
                FreeBytesAvailable = query.FreeBytesAvailable,
                TotalBytesAvailable = query.TotalBytesAvailable,
                LastHeartbeatAt = now,
                IsHealthy = true,
                RegisteredAt = now,
            };
            context.Set<StorageNodeEntity>().Add(existing);
        }
        else
        {
            existing.InternalHost = query.InternalHost.Trim();
            existing.InternalSshPort = query.InternalSshPort;
            existing.InternalHttpPort = query.InternalHttpPort;
            existing.FreeBytesAvailable = query.FreeBytesAvailable;
            existing.TotalBytesAvailable = query.TotalBytesAvailable;
            existing.LastHeartbeatAt = now;
            existing.IsHealthy = true;
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
}
