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

    public UserRegisterQueryHandler(
        IQueryProcessor queryProcessor,
        IEmailProtectionService emailProtectionService,
        IPasswordHasherService passwordHasherService
    )
    {
        _queryProcessor = queryProcessor;
        _emailProtectionService = emailProtectionService;
        _passwordHasherService = passwordHasherService;
    }

    public async Task<Option<UserId>> RunQueryAsync(
        UserRegisterQuery query,
        CancellationToken cancellationToken
    )
    {
        var usernameExists = await _queryProcessor
            .RunQueryAsync(
                new UserExistsByUsernameQuery { Username = query.Username },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (usernameExists.IsNone || usernameExists.Get())
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

        return await _queryProcessor
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
                    },
                },
                cancellationToken
            )
            .ConfigureAwait(false);
    }
}
