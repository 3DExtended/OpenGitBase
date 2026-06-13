using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.PublicGitSshKey.Contracts;
using OpenGitBase.Features.PublicGitSshKey.Entities;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.PublicGitSshKey.QueryHandlers;

public class GetUserIdBySshKeyFingerprintQueryHandler
    : IQueryHandler<GetUserIdBySshKeyFingerprintQuery, UserId>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public GetUserIdBySshKeyFingerprintQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<UserId>> RunQueryAsync(
        GetUserIdBySshKeyFingerprintQuery query,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(query.Fingerprint))
        {
            return Option<UserId>.None;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var ownerUserId = await context
            .Set<PublicGitSshKeyEntity>()
            .AsNoTracking()
            .Where(key => key.Fingerprint == query.Fingerprint)
            .Select(key => key.OwnerUserId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return ownerUserId == Guid.Empty
            ? Option<UserId>.None
            : Option.From(UserId.From(ownerUserId));
    }
}
