using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Contracts.Queries.Users;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Users.QueryHandlers.Users;

public class UserGetIdByEmailQueryHandler : IQueryHandler<UserGetIdByEmailQuery, UserId>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IEmailProtectionService _emailProtectionService;

    public UserGetIdByEmailQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IEmailProtectionService emailProtectionService
    )
    {
        _contextFactory = contextFactory;
        _emailProtectionService = emailProtectionService;
    }

    public async Task<Option<UserId>> RunQueryAsync(
        UserGetIdByEmailQuery query,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(query.Email))
        {
            return Option<UserId>.None;
        }

        var lookupHash = _emailProtectionService.ComputeLookupHash(query.Email);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var userId = await context
            .Set<UserCredentialsEntity>()
            .AsNoTracking()
            .Where(x => x.EmailLookupHash == lookupHash && !x.Deleted)
            .Select(x => x.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        return userId == Guid.Empty ? Option<UserId>.None : UserId.From(userId);
    }
}
