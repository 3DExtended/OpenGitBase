using System.Security.Cryptography;
using OpenGitBase.Common.SendGrid;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Contracts.Queries.Users;

namespace OpenGitBase.Features.Users.QueryHandlers.Users;

public class UserRegisterQueryHandler : IQueryHandler<UserRegisterQuery, UserId>
{
    private readonly IQueryProcessor _queryProcessor;
    private readonly IEmailProtectionService _emailProtectionService;
    private readonly IPasswordHasherService _passwordHasherService;
    private readonly ISystemClock _systemClock;

    public UserRegisterQueryHandler(
        IQueryProcessor queryProcessor,
        IEmailProtectionService emailProtectionService,
        IPasswordHasherService passwordHasherService,
        ISystemClock systemClock
    )
    {
        _queryProcessor = queryProcessor;
        _emailProtectionService = emailProtectionService;
        _passwordHasherService = passwordHasherService;
        _systemClock = systemClock;
    }

    public async Task<Option<UserId>> RunQueryAsync(
        UserRegisterQuery query,
        CancellationToken cancellationToken
    )
    {
        if (ReservedSlugValidator.IsReserved(query.Username))
        {
            return Option<UserId>.None;
        }

        var usernameExists = await _queryProcessor
            .RunQueryAsync(
                new UserExistsByUsernameQuery { Username = query.Username },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (usernameExists.IsSome)
        {
            return Option<UserId>.None;
        }

        var emailExists = await _queryProcessor
            .RunQueryAsync(new UserExistsByEmailQuery { Email = query.Email }, cancellationToken)
            .ConfigureAwait(false);

        if (emailExists.IsNone || emailExists.Get())
        {
            return Option<UserId>.None;
        }

        var verificationCode =
            $"{RandomNumberGenerator.GetInt32(100, 999)}-{RandomNumberGenerator.GetInt32(100, 999)}-{RandomNumberGenerator.GetInt32(100, 999)}";

        var result = await _queryProcessor
            .RunQueryAsync(
                new UserCreateQuery
                {
                    ModelToCreate = new User { Username = query.Username },
                    UserCredentials = new UserCredentials
                    {
                        Username = query.Username,
                        PasswordHash = _passwordHasherService.HashPassword(query.Password),
                        SignInProvider = false,
                        EmailCiphertext = _emailProtectionService.EncryptEmail(query.Email),
                        EmailLookupHash = _emailProtectionService.ComputeLookupHash(query.Email),
                        EmailVerified = false,
                        EmailVerificationTokenHash = _passwordHasherService.HashPassword(
                            verificationCode
                        ),
                        EmailVerificationTokenExpireDate = _systemClock.UtcNow.AddHours(24),
                    },
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (result.IsNone)
        {
            return Option<UserId>.None;
        }

        var emailResult = await _queryProcessor
            .RunQueryAsync(
                new EmailSendQuery
                {
                    To = new EmailAddress { Email = query.Email, Name = query.Username },
                    Subject = "Verify your email",
                    HtmlBody =
                        $"Hello {query.Username},<br><br>Your email verification code is <strong>{verificationCode}</strong>. It expires in 24 hours.",
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return emailResult.IsSome ? result : Option<UserId>.None;
    }
}
