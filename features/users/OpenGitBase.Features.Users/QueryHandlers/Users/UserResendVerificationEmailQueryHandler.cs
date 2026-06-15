using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.SendGrid;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Queries.Users;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Users.QueryHandlers.Users;

public class UserResendVerificationEmailQueryHandler
    : IQueryHandler<UserResendVerificationEmailQuery, Unit>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IQueryProcessor _queryProcessor;
    private readonly IEmailProtectionService _emailProtectionService;
    private readonly IPasswordHasherService _passwordHasherService;
    private readonly ISystemClock _systemClock;

    public UserResendVerificationEmailQueryHandler(
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
        UserResendVerificationEmailQuery query,
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
            || credentials.EmailVerified
            || string.IsNullOrEmpty(credentials.EmailCiphertext)
        )
        {
            return Option<Unit>.None;
        }

        var verificationCode =
            $"{RandomNumberGenerator.GetInt32(100, 999)}-{RandomNumberGenerator.GetInt32(100, 999)}-{RandomNumberGenerator.GetInt32(100, 999)}";
        credentials.EmailVerificationTokenHash = _passwordHasherService.HashPassword(
            verificationCode
        );
        credentials.EmailVerificationTokenExpireDate = _systemClock.UtcNow.AddHours(24);
        await context.SaveChangesAsync(cancellationToken);

        var decryptedEmail = _emailProtectionService.DecryptEmail(credentials.EmailCiphertext);
        var emailResult = await _queryProcessor
            .RunQueryAsync(
                new EmailSendQuery
                {
                    To = new EmailAddress { Email = decryptedEmail, Name = credentials.Username },
                    Subject = "Verify your email",
                    HtmlBody =
                        $"Hello {credentials.Username},<br><br>Your email verification code is <strong>{verificationCode}</strong>. It expires in 24 hours.",
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return emailResult.IsSome ? Unit.Value : Option<Unit>.None;
    }
}
