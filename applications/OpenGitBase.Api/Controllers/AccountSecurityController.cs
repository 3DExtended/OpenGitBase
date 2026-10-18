using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Auth;
using OpenGitBase.Common.Options;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Api.Controllers;

/// <summary>
/// Two-factor sign-in settings of the signed-in account. Every change needs a recent
/// re-authentication of the calling session, so a stolen session cannot turn 2FA on or off.
/// </summary>
[ApiController]
[Route("account")]
[Authorize]
public class AccountSecurityController : ControllerBase
{
    private readonly IQueryProcessor _queryProcessor;
    private readonly IUserContext _userContext;
    private readonly TwoFactorOptions _twoFactorOptions;
    private readonly SessionIssuer _sessionIssuer;

    public AccountSecurityController(
        IQueryProcessor queryProcessor,
        IUserContext userContext,
        TwoFactorOptions twoFactorOptions,
        SessionIssuer sessionIssuer
    )
    {
        _queryProcessor = queryProcessor;
        _userContext = userContext;
        _twoFactorOptions = twoFactorOptions;
        _sessionIssuer = sessionIssuer;
    }

    private UserId CurrentUserId => UserId.From(_userContext.User.UserId);

    [HttpGet("security")]
    public async Task<ActionResult<AccountSecurityResponse>> GetSecurityAsync(CancellationToken cancellationToken)
    {
        var result = await _queryProcessor
            .RunQueryAsync(new GetAccountSecurityQuery { UserId = CurrentUserId }, cancellationToken)
            .ConfigureAwait(false);
        if (result.IsNone)
        {
            return NotFound();
        }

        var status = result.Get();
        return Ok(
            new AccountSecurityResponse
            {
                TwoFactorAvailable = _twoFactorOptions.Enabled,
                TwoFactorEnabled = status.TwoFactorEnabled,
                TotpConfigured = status.TotpConfigured,
                HasPassword = status.HasPassword,
                RecoveryCodesRemaining = status.RecoveryCodesRemaining,
            }
        );
    }

    /// <summary>
    /// Confirms the caller again for 10 minutes: the password, plus a code or a recovery code while 2FA is on.
    /// </summary>
    [HttpPost("reauthenticate")]
    [EnableRateLimiting("sensitive")]
    public async Task<ActionResult> ReauthenticateAsync(
        [FromBody] ReauthenticateDto requestDto,
        CancellationToken cancellationToken
    )
    {
        var sessionId = SessionClaims.ReadSessionId(User);
        if (requestDto == null || string.IsNullOrEmpty(sessionId))
        {
            return BadRequest();
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new ReauthenticateQuery
                {
                    UserId = CurrentUserId,
                    SessionId = sessionId,
                    Password = requestDto.Password,
                    Code = requestDto.Code?.Trim(),
                    RecoveryCode = requestDto.RecoveryCode,
                },
                cancellationToken
            )
            .ConfigureAwait(false);
        if (result.IsNone)
        {
            return NotFound();
        }

        var outcome = result.Get();
        return outcome.Status switch
        {
            ReauthenticationStatus.Succeeded => NoContent(),
            ReauthenticationStatus.InvalidCredentials => Unauthorized(Error(SecondFactorErrorResponse.InvalidCredentials)),
            ReauthenticationStatus.SecondFactorRequired => Unauthorized(
                Error(SecondFactorErrorResponse.SecondFactorRequired)
            ),
            ReauthenticationStatus.InvalidCode => Unauthorized(Error(SecondFactorErrorResponse.InvalidCode)),
            ReauthenticationStatus.LockedOut => StatusCode(
                StatusCodes.Status429TooManyRequests,
                new SecondFactorErrorResponse
                {
                    Error = SecondFactorErrorResponse.LockedOut,
                    LockedUntil = outcome.LockedUntil,
                }
            ),
            _ => BadRequest(Error(SecondFactorErrorResponse.ReauthenticationUnavailable)),
        };
    }

    /// <summary>Starts (or restarts) authenticator setup; the current authenticator keeps working until confirmed.</summary>
    [HttpPost("two-factor/totp/setup")]
    public async Task<ActionResult<TotpSetupResponse>> StartTotpSetupAsync(CancellationToken cancellationToken)
    {
        if (!_twoFactorOptions.Enabled)
        {
            return NotFound();
        }

        if (!await HasRecentReauthenticationAsync(cancellationToken).ConfigureAwait(false))
        {
            return ReauthenticationRequired();
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new StartTotpSetupQuery { UserId = CurrentUserId, Issuer = _twoFactorOptions.Issuer },
                cancellationToken
            )
            .ConfigureAwait(false);
        if (result.IsNone)
        {
            return NotFound();
        }

        return Ok(new TotpSetupResponse { OtpauthUri = result.Get().OtpauthUri, Secret = result.Get().Secret });
    }

    /// <summary>Confirms the authenticator with its first code; the first confirmation turns 2FA on.</summary>
    [HttpPost("two-factor/totp/confirm")]
    public async Task<ActionResult<TwoFactorStatusResponse>> ConfirmTotpSetupAsync(
        [FromBody] ConfirmTotpSetupDto requestDto,
        CancellationToken cancellationToken
    )
    {
        if (!_twoFactorOptions.Enabled)
        {
            return NotFound();
        }

        if (!await HasRecentReauthenticationAsync(cancellationToken).ConfigureAwait(false))
        {
            return ReauthenticationRequired();
        }

        if (requestDto == null || string.IsNullOrWhiteSpace(requestDto.Code))
        {
            return BadRequest(Error(SecondFactorErrorResponse.InvalidCode));
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new ConfirmTotpSetupQuery { UserId = CurrentUserId, Code = requestDto.Code.Trim() },
                cancellationToken
            )
            .ConfigureAwait(false);
        if (result.IsNone)
        {
            return NotFound();
        }

        var confirmation = result.Get();
        switch (confirmation.Status)
        {
            case TotpConfirmationStatus.Confirmed:
                if (confirmation.TwoFactorNewlyEnabled)
                {
                    // Turning 2FA on revoked every session, this one included. Keep only the caller signed in.
                    await _sessionIssuer.IssueAsync(Response, CurrentUserId, cancellationToken).ConfigureAwait(false);
                }

                return Ok(
                    new TwoFactorStatusResponse { TwoFactorEnabled = true, RecoveryCodes = confirmation.RecoveryCodes }
                );
            case TotpConfirmationStatus.NoPendingSetup:
                return BadRequest(Error(SecondFactorErrorResponse.NoPendingSetup));
            default:
                return BadRequest(Error(SecondFactorErrorResponse.InvalidCode));
        }
    }

    /// <summary>Turns 2FA off and removes the authenticator. Stays available while the switch is off.</summary>
    [HttpPost("two-factor/disable")]
    public async Task<ActionResult<TwoFactorStatusResponse>> DisableTwoFactorAsync(CancellationToken cancellationToken)
    {
        if (!await HasRecentReauthenticationAsync(cancellationToken).ConfigureAwait(false))
        {
            return ReauthenticationRequired();
        }

        var result = await _queryProcessor
            .RunQueryAsync(new DisableTwoFactorQuery { UserId = CurrentUserId }, cancellationToken)
            .ConfigureAwait(false);
        if (result.IsNone)
        {
            return NotFound();
        }

        await _sessionIssuer.IssueAsync(Response, CurrentUserId, cancellationToken).ConfigureAwait(false);
        return Ok(new TwoFactorStatusResponse { TwoFactorEnabled = false });
    }

    /// <summary>
    /// Replaces every recovery code with a new set, shown once. Stays available while the switch is off,
    /// like disabling, so accounts that already have 2FA keep a way back in.
    /// </summary>
    [HttpPost("two-factor/recovery-codes")]
    public async Task<ActionResult<RecoveryCodesResponse>> RegenerateRecoveryCodesAsync(
        CancellationToken cancellationToken
    )
    {
        if (!await HasRecentReauthenticationAsync(cancellationToken).ConfigureAwait(false))
        {
            return ReauthenticationRequired();
        }

        var result = await _queryProcessor
            .RunQueryAsync(new RegenerateRecoveryCodesQuery { UserId = CurrentUserId }, cancellationToken)
            .ConfigureAwait(false);
        if (result.IsNone)
        {
            return BadRequest(Error(SecondFactorErrorResponse.TwoFactorNotEnabled));
        }

        return Ok(new RecoveryCodesResponse { RecoveryCodes = result.Get().Codes });
    }

    private static SecondFactorErrorResponse Error(string error) => new() { Error = error };

    private ObjectResult ReauthenticationRequired() =>
        StatusCode(StatusCodes.Status403Forbidden, Error(SecondFactorErrorResponse.ReauthenticationRequired));

    private async Task<bool> HasRecentReauthenticationAsync(CancellationToken cancellationToken)
    {
        var sessionId = SessionClaims.ReadSessionId(User);
        if (string.IsNullOrEmpty(sessionId) || !SessionClaims.TryReadIssuedAt(User, out var issuedAt))
        {
            return false;
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new HasRecentReauthenticationQuery
                {
                    UserId = CurrentUserId,
                    SessionId = sessionId,
                    SessionIssuedAt = issuedAt,
                },
                cancellationToken
            )
            .ConfigureAwait(false);
        return result.IsSome && result.Get();
    }
}
