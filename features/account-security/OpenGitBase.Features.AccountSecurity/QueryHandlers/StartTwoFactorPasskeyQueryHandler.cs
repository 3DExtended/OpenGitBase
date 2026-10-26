using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.AccountSecurity.Challenges;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.Entities;
using OpenGitBase.Features.AccountSecurity.Passkeys;

namespace OpenGitBase.Features.AccountSecurity.QueryHandlers;

public class StartTwoFactorPasskeyQueryHandler : IQueryHandler<StartTwoFactorPasskeyQuery, TwoFactorPasskeyOptions>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly WebAuthnOptions _webAuthnOptions;
    private readonly ISystemClock _clock;

    public StartTwoFactorPasskeyQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        WebAuthnOptions webAuthnOptions,
        ISystemClock clock
    )
    {
        _contextFactory = contextFactory;
        _webAuthnOptions = webAuthnOptions;
        _clock = clock;
    }

    public async Task<Option<TwoFactorPasskeyOptions>> RunQueryAsync(
        StartTwoFactorPasskeyQuery query,
        CancellationToken cancellationToken
    )
    {
        var now = _clock.UtcNow;
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var challenge = await AuthenticationChallenges.FindActiveAsync(
            context,
            AuthenticationChallengePurpose.TwoFactorSignIn,
            query.ChallengeToken,
            now,
            cancellationToken
        );
        if (challenge?.UserId is not { } userId)
        {
            return new TwoFactorPasskeyOptions(TwoFactorPasskeyOptionsStatus.ChallengeExpired);
        }

        var passkeys = await PasskeyCeremonies.DescriptorsAsync(context, userId, cancellationToken);
        if (passkeys.Count == 0)
        {
            return new TwoFactorPasskeyOptions(TwoFactorPasskeyOptionsStatus.NoPasskeys);
        }

        var options = PasskeyCeremonies
            .CreateRelyingParty(_webAuthnOptions)
            .GetAssertionOptions(
                new GetAssertionOptionsParams
                {
                    AllowedCredentials = passkeys,
                    UserVerification = UserVerificationRequirement.Preferred,
                }
            );
        var optionsJson = options.ToJson();
        await PasskeyCeremonies.StoreChallengeAsync(
            context,
            AuthenticationChallengePurpose.PasskeySecondFactor,
            userId,
            options.Challenge,
            optionsJson,
            now,
            cancellationToken
        );
        return new TwoFactorPasskeyOptions(TwoFactorPasskeyOptionsStatus.Issued, optionsJson);
    }
}
