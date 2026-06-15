using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Security;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.StorageNode.Entities;

namespace OpenGitBase.Features.StorageNode.QueryHandlers;

public sealed class VerifyStorageNodeTokenQueryHandler
    : IQueryHandler<VerifyStorageNodeTokenQuery, StorageNodeId>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IPasswordHasherService _passwordHasherService;

    public VerifyStorageNodeTokenQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IPasswordHasherService passwordHasherService
    )
    {
        _contextFactory = contextFactory;
        _passwordHasherService = passwordHasherService;
    }

    public async Task<Option<StorageNodeId>> RunQueryAsync(
        VerifyStorageNodeTokenQuery query,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(query.NodeId) || string.IsNullOrWhiteSpace(query.ApiToken))
        {
            return Option<StorageNodeId>.None;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var node = await context
            .Set<StorageNodeEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.NodeId == query.NodeId, cancellationToken)
            .ConfigureAwait(false);

        if (node is null)
        {
            return Option<StorageNodeId>.None;
        }

        if (
            !NodeCertificateThumbprint.Matches(
                node.CertificateThumbprint,
                query.CertificateThumbprint
            )
        )
        {
            return Option<StorageNodeId>.None;
        }

        return _passwordHasherService.VerifyPassword(node.ApiTokenHash, query.ApiToken)
            ? Option.From(StorageNodeId.From(node.Id))
            : Option<StorageNodeId>.None;
    }
}
