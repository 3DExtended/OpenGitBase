using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.Entities;
using OpenGitBase.Features.AccountSecurity.Passkeys;

namespace OpenGitBase.Features.AccountSecurity.QueryHandlers;

public class StartPasskeySignInQueryHandler : IQueryHandler<StartPasskeySignInQuery, PasskeyCeremonyOptions>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly WebAuthnOptions _webAuthnOptions;
    private readonly ISystemClock _clock;

    public StartPasskeySignInQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        WebAuthnOptions webAuthnOptions,
        ISystemClock clock
    )
    {
        _contextFactory = contextFactory;
        _webAuthnOptions = webAuthnOptions;
        _clock = clock;
    }

    public async Task<Option<PasskeyCeremonyOptions>> RunQueryAsync(
        StartPasskeySignInQuery query,
        CancellationToken cancellationToken
    )
    {
        var options = PasskeyCeremonies
            .CreateRelyingParty(_webAuthnOptions)
            .GetAssertionOptions(
                new GetAssertionOptionsParams
                {
                    AllowedCredentials = [],
                    UserVerification = UserVerificationRequirement.Required,
                }
            );
        var optionsJson = options.ToJson();
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await PasskeyCeremonies.StoreChallengeAsync(
            context,
            AuthenticationChallengePurpose.PasskeySignIn,
            null,
            options.Challenge,
            optionsJson,
            _clock.UtcNow,
            cancellationToken
        );
        return new PasskeyCeremonyOptions(optionsJson);
    }
}
