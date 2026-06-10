using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.SendGrid;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Queries.Users;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Users.QueryHandlers.Users;

public class UserRequestPasswordResetQueryHandler
    : IQueryHandler<UserRequestPasswordResetQuery, Unit>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IQueryProcessor _queryProcessor;
    private readonly IEmailProtectionService _emailProtectionService;
    private readonly IPasswordHasherService _passwordHasherService;
    private readonly ISystemClock _systemClock;

    public UserRequestPasswordResetQueryHandler(
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
        UserRequestPasswordResetQuery query,
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
        )
        {
            return Option<Unit>.None;
        }

        var decryptedEmail = _emailProtectionService.DecryptEmail(credentials.EmailCiphertext);
        if (!string.Equals(decryptedEmail, query.Email.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return Option<Unit>.None;
        }

        var resetCode =
            $"{RandomNumberGenerator.GetInt32(100, 999)}-{RandomNumberGenerator.GetInt32(100, 999)}-{RandomNumberGenerator.GetInt32(100, 999)}";
        credentials.PasswordResetTokenHash = _passwordHasherService.HashPassword(resetCode);
        credentials.PasswordResetTokenExpireDate = _systemClock.UtcNow.AddHours(2);
        await context.SaveChangesAsync(cancellationToken);

        var emailResult = await _queryProcessor
            .RunQueryAsync(
                new EmailSendQuery
                {
                    To = new EmailAddress { Email = decryptedEmail, Name = query.Username },
                    Subject = "Password reset",
                    HtmlBody =
                        $"Hello {query.Username},<br><br>Your password reset code is <strong>{resetCode}</strong>. It expires in 2 hours.",
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return emailResult.IsSome ? Unit.Value : Option<Unit>.None;
    }
}
