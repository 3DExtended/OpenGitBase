using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Queries.Users;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Users.QueryHandlers.Users;

public class UserChangePasswordQueryHandler : IQueryHandler<UserChangePasswordQuery, Unit>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IPasswordHasherService _passwordHasherService;

    public UserChangePasswordQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IPasswordHasherService passwordHasherService
    )
    {
        _contextFactory = contextFactory;
        _passwordHasherService = passwordHasherService;
    }

    public async Task<Option<Unit>> RunQueryAsync(
        UserChangePasswordQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var credentials = await context
            .Set<UserCredentialsEntity>()
            .FirstOrDefaultAsync(
                x => x.UserId == query.UserId.Value && !x.Deleted,
                cancellationToken
            );

        if (
            credentials == null
            || credentials.SignInProvider
            || string.IsNullOrEmpty(credentials.PasswordHash)
            || !_passwordHasherService.VerifyPassword(
                credentials.PasswordHash,
                query.CurrentPassword
            )
        )
        {
            return Option<Unit>.None;
        }

        credentials.PasswordHash = _passwordHasherService.HashPassword(query.NewPassword);
        await context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
