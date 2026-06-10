using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Contracts.Queries.Users;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Users.QueryHandlers.Users;

public class UserLoginQueryHandler : IQueryHandler<UserLoginQuery, UserId>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IPasswordHasherService _passwordHasherService;

    public UserLoginQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IPasswordHasherService passwordHasherService
    )
    {
        _contextFactory = contextFactory;
        _passwordHasherService = passwordHasherService;
    }

    public async Task<Option<UserId>> RunQueryAsync(
        UserLoginQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var credentials = await context
            .Set<UserCredentialsEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Username == query.Username && !x.Deleted,
                cancellationToken
            );

        if (credentials == null || credentials.SignInProvider)
        {
            return Option<UserId>.None;
        }

        if (
            string.IsNullOrEmpty(credentials.PasswordHash)
            || !_passwordHasherService.VerifyPassword(credentials.PasswordHash, query.Password)
        )
        {
            return Option<UserId>.None;
        }

        return UserId.From(credentials.UserId);
    }
}
