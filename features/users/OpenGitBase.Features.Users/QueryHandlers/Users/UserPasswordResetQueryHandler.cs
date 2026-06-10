using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.SendGrid;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Queries.Users;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Users.QueryHandlers.Users;

public class UserPasswordResetQueryHandler : IQueryHandler<UserPasswordResetQuery, Unit>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IQueryProcessor _queryProcessor;
    private readonly IEmailProtectionService _emailProtectionService;
    private readonly IPasswordHasherService _passwordHasherService;
    private readonly ISystemClock _systemClock;

    public UserPasswordResetQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IQueryProcessor queryProcessor,
        IEmailProtectionService emailProtectionService,
        IPasswordHasherService passwordHasherService,
        ISystemClock systemClock
    )
    {
        _contextFactory = contextFactory;
        _queryProcessor = queryProcessor;
        _emailProtectionService = emailProtectionService;
        _passwordHasherService = passwordHasherService;
        _systemClock = systemClock;
    }

    public async Task<Option<Unit>> RunQueryAsync(
        UserPasswordResetQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var credentials = await context
            .Set<UserCredentialsEntity>()
            .FirstOrDefaultAsync(
                x => x.Username == query.Username && !x.Deleted,
                cancellationToken
            );

        if (
            credentials == null
            || credentials.SignInProvider
            || string.IsNullOrEmpty(credentials.EmailCiphertext)
            || string.IsNullOrEmpty(credentials.PasswordResetTokenHash)
            || credentials.PasswordResetTokenExpireDate == null
        )
        {
            return Option<Unit>.None;
        }

        var decryptedEmail = _emailProtectionService.DecryptEmail(credentials.EmailCiphertext);
        if (!string.Equals(decryptedEmail, query.Email.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return Option<Unit>.None;
        }

        if (credentials.PasswordResetTokenExpireDate < _systemClock.UtcNow)
        {
            return Option<Unit>.None;
        }

        if (
            !_passwordHasherService.VerifyPassword(
                credentials.PasswordResetTokenHash,
                query.ResetCode
            )
        )
        {
            return Option<Unit>.None;
        }

        credentials.PasswordHash = _passwordHasherService.HashPassword(query.NewPassword);
        credentials.PasswordResetTokenHash = null;
        credentials.PasswordResetTokenExpireDate = null;
        await context.SaveChangesAsync(cancellationToken);

        var emailResult = await _queryProcessor
            .RunQueryAsync(
                new EmailSendQuery
                {
                    To = new EmailAddress { Email = decryptedEmail, Name = query.Username },
                    Subject = "Password changed",
                    HtmlBody =
                        $"Hello {query.Username},<br><br>Your password was changed successfully.",
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return emailResult.IsSome ? Unit.Value : Option<Unit>.None;
    }
}
