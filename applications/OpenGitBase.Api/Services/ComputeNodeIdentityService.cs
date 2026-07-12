using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Features.ComputeNode;
using OpenGitBase.Features.ComputeNode.Entities;

namespace OpenGitBase.Api.Services;

public sealed class ComputeNodeIdentityService
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IPasswordHasherService _passwordHasherService;

    public ComputeNodeIdentityService(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IPasswordHasherService passwordHasherService
    )
    {
        _contextFactory = contextFactory;
        _passwordHasherService = passwordHasherService;
    }

    public async Task<ComputeNodeEntity?> AuthenticateAsync(
        string? bearerToken,
        CancellationToken cancellationToken
    )
    {
        if (!ComputeNodeIdentityTokens.TryParseNodeId(bearerToken ?? string.Empty, out var nodeId))
        {
            return null;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var node = await context
            .Set<ComputeNodeEntity>()
            .FirstOrDefaultAsync(entity => entity.Id == nodeId, cancellationToken)
            .ConfigureAwait(false);
        if (
            node is null
            || string.IsNullOrWhiteSpace(node.IdentityTokenHash)
            || string.IsNullOrWhiteSpace(bearerToken)
            || !_passwordHasherService.VerifyPassword(node.IdentityTokenHash, bearerToken)
        )
        {
            return null;
        }

        return node;
    }
}
