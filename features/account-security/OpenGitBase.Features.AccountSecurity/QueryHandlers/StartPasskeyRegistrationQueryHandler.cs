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
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.AccountSecurity.QueryHandlers;

public class StartPasskeyRegistrationQueryHandler : IQueryHandler<StartPasskeyRegistrationQuery, PasskeyCeremonyOptions>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly WebAuthnOptions _webAuthnOptions;
    private readonly ISystemClock _clock;

    public StartPasskeyRegistrationQueryHandler(
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
        StartPasskeyRegistrationQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var username = await context
            .Set<UserCredentialsEntity>()
            .AsNoTracking()
            .Where(c => c.UserId == query.UserId.Value && !c.Deleted)
            .Select(c => c.Username)
            .FirstOrDefaultAsync(cancellationToken);
        if (username is null)
        {
            return Option<PasskeyCeremonyOptions>.None;
        }

        var options = PasskeyCeremonies
            .CreateRelyingParty(_webAuthnOptions)
            .RequestNewCredential(
                new RequestNewCredentialParams
                {
                    // The user handle is the account id, so a usernameless sign-in can find the account.
                    User = new Fido2User
                    {
                        Id = query.UserId.Value.ToByteArray(),
                        Name = username,
                        DisplayName = username,
                    },
                    ExcludeCredentials = await PasskeyCeremonies.DescriptorsAsync(
                        context,
                        query.UserId.Value,
                        cancellationToken
                    ),
                    AuthenticatorSelection = new AuthenticatorSelection
                    {
                        ResidentKey = ResidentKeyRequirement.Required,
                        UserVerification = UserVerificationRequirement.Required,
                    },
                    AttestationPreference = AttestationConveyancePreference.None,
                }
            );
        var optionsJson = options.ToJson();
        await PasskeyCeremonies.StoreChallengeAsync(
            context,
            AuthenticationChallengePurpose.PasskeyRegistration,
            query.UserId.Value,
            options.Challenge,
            optionsJson,
            _clock.UtcNow,
            cancellationToken
        );
        return new PasskeyCeremonyOptions(optionsJson);
    }
}
