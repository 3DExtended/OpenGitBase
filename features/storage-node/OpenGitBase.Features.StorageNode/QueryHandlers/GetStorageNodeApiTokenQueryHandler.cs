using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.StorageNode.Entities;

namespace OpenGitBase.Features.StorageNode.QueryHandlers;

public sealed class GetStorageNodeApiTokenQueryHandler
    : IQueryHandler<GetStorageNodeApiTokenQuery, string>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IEmailProtectionService _emailProtectionService;

    public GetStorageNodeApiTokenQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IEmailProtectionService emailProtectionService
    )
    {
        _contextFactory = contextFactory;
        _emailProtectionService = emailProtectionService;
    }

    public async Task<Option<string>> RunQueryAsync(
        GetStorageNodeApiTokenQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var node = await context
            .Set<StorageNodeEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == query.StorageNodeId.Value, cancellationToken)
            .ConfigureAwait(false);

        if (node is null || string.IsNullOrWhiteSpace(node.ApiTokenProtected))
        {
            return Option<string>.None;
        }

        try
        {
            return Option.From(_emailProtectionService.DecryptSecret(node.ApiTokenProtected));
        }
        catch (FormatException)
        {
            return Option<string>.None;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return Option<string>.None;
        }
    }
}
