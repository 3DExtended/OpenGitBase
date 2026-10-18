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

public class StartPasskeyReauthenticationQueryHandler
    : IQueryHandler<StartPasskeyReauthenticationQuery, PasskeyCeremonyOptions>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly WebAuthnOptions _webAuthnOptions;
    private readonly ISystemClock _clock;

    public StartPasskeyReauthenticationQueryHandler(
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
        StartPasskeyReauthenticationQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var passkeys = await PasskeyCeremonies.DescriptorsAsync(context, query.UserId.Value, cancellationToken);
        if (passkeys.Count == 0)
        {
            return Option<PasskeyCeremonyOptions>.None;
        }

        var options = PasskeyCeremonies
            .CreateRelyingParty(_webAuthnOptions)
            .GetAssertionOptions(
                new GetAssertionOptionsParams
                {
                    AllowedCredentials = passkeys,
                    // Preferred, so a security key also works together with the password; on its own the
                    // assertion must still carry the UV flag (see ReauthenticateWithPasskeyQueryHandler).
                    UserVerification = UserVerificationRequirement.Preferred,
                }
            );
        var optionsJson = options.ToJson();
        await PasskeyCeremonies.StoreChallengeAsync(
            context,
            AuthenticationChallengePurpose.PasskeyReauthentication,
            query.UserId.Value,
            options.Challenge,
            optionsJson,
            _clock.UtcNow,
            cancellationToken
        );
        return new PasskeyCeremonyOptions(optionsJson);
    }
}
