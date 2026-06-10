using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Contracts.Queries.Users;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Users.QueryHandlers.Users;

public class UserCreateQueryHandler : IQueryHandler<UserCreateQuery, UserId>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IQueryProcessor _queryProcessor;
    private readonly ISystemClock _systemClock;

    public UserCreateQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IQueryProcessor queryProcessor,
        ISystemClock systemClock
    )
    {
        _contextFactory = contextFactory;
        _queryProcessor = queryProcessor;
        _systemClock = systemClock;
    }

    public async Task<Option<UserId>> RunQueryAsync(
        UserCreateQuery query,
        CancellationToken cancellationToken
    )
    {
        var userId = Guid.NewGuid();

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        context
            .Set<UserEntity>()
            .Add(
                new UserEntity
                {
                    Id = userId,
                    Username = query.ModelToCreate.Username,
                    NormalizedUsername = query.ModelToCreate.Username.Trim().ToLowerInvariant(),
                    CreatedAt = _systemClock.UtcNow,
                }
            );
        await context.SaveChangesAsync(cancellationToken);

        if (query.UserCredentials != null)
        {
            query.UserCredentials.Id = UserCredentialsId.From(userId);
            var credentialsResult = await _queryProcessor
                .RunQueryAsync(
                    new UserCredentialsCreateQuery { ModelToCreate = query.UserCredentials },
                    cancellationToken
                )
                .ConfigureAwait(false);

            if (credentialsResult.IsNone)
            {
                return Option<UserId>.None;
            }
        }

        return UserId.From(userId);
    }
}
