using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.Entities;
using OpenGitBase.Features.AccountSecurity.Passkeys;

namespace OpenGitBase.Features.AccountSecurity.QueryHandlers;

public class CompletePasskeyRegistrationQueryHandler
    : IQueryHandler<CompletePasskeyRegistrationQuery, PasskeyRegistrationResult>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly WebAuthnOptions _webAuthnOptions;
    private readonly ISystemClock _clock;
    private readonly ILogger<CompletePasskeyRegistrationQueryHandler> _logger;

    public CompletePasskeyRegistrationQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        WebAuthnOptions webAuthnOptions,
        ISystemClock clock,
        ILogger<CompletePasskeyRegistrationQueryHandler> logger
    )
    {
        _contextFactory = contextFactory;
        _webAuthnOptions = webAuthnOptions;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Option<PasskeyRegistrationResult>> RunQueryAsync(
        CompletePasskeyRegistrationQuery query,
        CancellationToken cancellationToken
    )
    {
        var now = _clock.UtcNow;
        var response = PasskeyCeremonies.Parse<AuthenticatorAttestationRawResponse>(query.AttestationResponseJson);
        if (response?.Response is null)
        {
            return new PasskeyRegistrationResult(PasskeyRegistrationStatus.Rejected);
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var optionsJson = await PasskeyCeremonies.TakeChallengeAsync(
            context,
            AuthenticationChallengePurpose.PasskeyRegistration,
            query.UserId.Value,
            response.Response.ClientDataJson,
            now,
            cancellationToken
        );
        if (optionsJson is null)
        {
            return new PasskeyRegistrationResult(PasskeyRegistrationStatus.ChallengeExpired);
        }

        var duplicate = false;
        RegisteredPublicKeyCredential credential;
        try
        {
            credential = await PasskeyCeremonies
                .CreateRelyingParty(_webAuthnOptions)
                .MakeNewCredentialAsync(
                    new MakeNewCredentialParams
                    {
                        AttestationResponse = response,
                        OriginalOptions = CredentialCreateOptions.FromJson(optionsJson),
                        IsCredentialIdUniqueToUserCallback = async (candidate, token) =>
                        {
                            duplicate = await context
                                .Set<UserPasskeyEntity>()
                                .AnyAsync(passkey => passkey.CredentialId == candidate.CredentialId, token);
                            return !duplicate;
                        },
                    },
                    cancellationToken
                );
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogInformation(
                "Passkey registration for user {UserId} rejected: {Reason}",
                query.UserId.Value,
                exception.Message
            );
            return new PasskeyRegistrationResult(
                duplicate ? PasskeyRegistrationStatus.AlreadyRegistered : PasskeyRegistrationStatus.Rejected
            );
        }

        var passkey = new UserPasskeyEntity
        {
            Id = Guid.NewGuid(),
            UserId = query.UserId.Value,
            CredentialId = credential.Id,
            PublicKey = credential.PublicKey,
            SignCount = credential.SignCount,
            Aaguid = credential.AaGuid,
            Transports = PasskeyCeremonies.FormatTransports(credential.Transports),
            IsBackupEligible = credential.IsBackupEligible,
            IsBackedUp = credential.IsBackedUp,
            Name = query.Name.Trim(),
            CreatedAt = now,
        };
        context.Set<UserPasskeyEntity>().Add(passkey);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another request registered the same credential between the check and this insert.
            return new PasskeyRegistrationResult(PasskeyRegistrationStatus.AlreadyRegistered);
        }

        _logger.LogInformation("Passkey {PasskeyId} added for user {UserId}", passkey.Id, query.UserId.Value);
        return new PasskeyRegistrationResult(PasskeyRegistrationStatus.Registered, PasskeyCeremonies.Summarize(passkey));
    }
}
