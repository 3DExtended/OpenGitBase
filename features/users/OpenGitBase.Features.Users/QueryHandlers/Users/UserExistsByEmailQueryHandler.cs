using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Queries.Users;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Users.QueryHandlers.Users;

public class UserExistsByEmailQueryHandler : IQueryHandler<UserExistsByEmailQuery, bool>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IEmailProtectionService _emailProtectionService;

    public UserExistsByEmailQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IEmailProtectionService emailProtectionService
    )
    {
        _contextFactory = contextFactory;
        _emailProtectionService = emailProtectionService;
    }

    public async Task<Option<bool>> RunQueryAsync(
        UserExistsByEmailQuery query,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(query.Email))
        {
            return Option<bool>.None;
        }

        var lookupHash = _emailProtectionService.ComputeLookupHash(query.Email);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var exists = await context
            .Set<UserCredentialsEntity>()
            .AsNoTracking()
            .AnyAsync(x => x.EmailLookupHash == lookupHash && !x.Deleted, cancellationToken);

        return exists;
    }
}
