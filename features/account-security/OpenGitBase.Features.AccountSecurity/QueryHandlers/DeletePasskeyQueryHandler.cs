using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.Entities;

namespace OpenGitBase.Features.AccountSecurity.QueryHandlers;

public class DeletePasskeyQueryHandler : IQueryHandler<DeletePasskeyQuery, Unit>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly ILogger<DeletePasskeyQueryHandler> _logger;

    public DeletePasskeyQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        ILogger<DeletePasskeyQueryHandler> logger
    )
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<Option<Unit>> RunQueryAsync(DeletePasskeyQuery query, CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var deleted = await context
            .Set<UserPasskeyEntity>()
            .Where(passkey => passkey.Id == query.PasskeyId && passkey.UserId == query.UserId.Value)
            .ExecuteDeleteAsync(cancellationToken);
        if (deleted == 0)
        {
            return Option<Unit>.None;
        }

        _logger.LogInformation("Passkey {PasskeyId} removed for user {UserId}", query.PasskeyId, query.UserId.Value);
        return Unit.Value;
    }
}
