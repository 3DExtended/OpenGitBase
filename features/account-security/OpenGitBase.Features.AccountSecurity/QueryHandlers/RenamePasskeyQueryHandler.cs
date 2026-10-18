using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.Entities;
using OpenGitBase.Features.AccountSecurity.Passkeys;

namespace OpenGitBase.Features.AccountSecurity.QueryHandlers;

public class RenamePasskeyQueryHandler : IQueryHandler<RenamePasskeyQuery, PasskeySummary>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public RenamePasskeyQueryHandler(IDbContextFactory<OpenGitBaseDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<PasskeySummary>> RunQueryAsync(RenamePasskeyQuery query, CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var passkey = await context
            .Set<UserPasskeyEntity>()
            .FirstOrDefaultAsync(
                passkey => passkey.Id == query.PasskeyId && passkey.UserId == query.UserId.Value,
                cancellationToken
            );
        if (passkey is null)
        {
            return Option<PasskeySummary>.None;
        }

        passkey.Name = query.Name.Trim();
        await context.SaveChangesAsync(cancellationToken);
        return PasskeyCeremonies.Summarize(passkey);
    }
}
