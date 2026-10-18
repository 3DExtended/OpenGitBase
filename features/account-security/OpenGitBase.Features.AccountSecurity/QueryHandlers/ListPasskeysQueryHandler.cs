using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.Entities;
using OpenGitBase.Features.AccountSecurity.Passkeys;

namespace OpenGitBase.Features.AccountSecurity.QueryHandlers;

public class ListPasskeysQueryHandler : IQueryHandler<ListPasskeysQuery, PasskeyList>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public ListPasskeysQueryHandler(IDbContextFactory<OpenGitBaseDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<PasskeyList>> RunQueryAsync(ListPasskeysQuery query, CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var passkeys = await context
            .Set<UserPasskeyEntity>()
            .AsNoTracking()
            .Where(passkey => passkey.UserId == query.UserId.Value)
            .ToListAsync(cancellationToken);

        // Sorted in memory: SQLite, used by the tests, cannot order by DateTimeOffset.
        return new PasskeyList(
            passkeys.OrderBy(passkey => passkey.CreatedAt).Select(PasskeyCeremonies.Summarize).ToList()
        );
    }
}
