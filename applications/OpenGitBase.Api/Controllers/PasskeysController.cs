using System.Text.Json;
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
/// The signed-in account's passkeys. Adding and removing one needs a recent re-authentication of the
/// calling session; renaming does not. Every endpoint is missing while <c>WebAuthn:RelyingPartyId</c> is empty.
/// </summary>
[ApiController]
[Route("account/passkeys")]
[Authorize]
public class PasskeysController : ControllerBase
{
    private const int MaxNameLength = 64;

    private readonly IQueryProcessor _queryProcessor;
    private readonly IUserContext _userContext;
    private readonly WebAuthnOptions _webAuthnOptions;

    public PasskeysController(IQueryProcessor queryProcessor, IUserContext userContext, WebAuthnOptions webAuthnOptions)
    {
        _queryProcessor = queryProcessor;
        _userContext = userContext;
        _webAuthnOptions = webAuthnOptions;
    }

    private UserId CurrentUserId => UserId.From(_userContext.User.UserId);

    [HttpGet("")]
    public async Task<ActionResult<IReadOnlyList<PasskeyResponse>>> ListAsync(CancellationToken cancellationToken)
    {
        if (!_webAuthnOptions.Enabled)
        {
            return NotFound();
        }

        var result = await _queryProcessor
            .RunQueryAsync(new ListPasskeysQuery { UserId = CurrentUserId }, cancellationToken)
            .ConfigureAwait(false);
        return Ok(result.IsNone ? [] : result.Get().Passkeys.Select(ToResponse).ToList());
    }

    /// <summary>Creation options for <c>navigator.credentials.create()</c>; the challenge is valid for 5 minutes.</summary>
    [HttpPost("registration-options")]
    [EnableRateLimiting("sensitive")]
    public async Task<ActionResult> StartRegistrationAsync(CancellationToken cancellationToken)
    {
        if (!_webAuthnOptions.Enabled)
        {
            return NotFound();
        }

        if (!await HasRecentReauthenticationAsync(cancellationToken).ConfigureAwait(false))
        {
            return ReauthenticationRequired();
        }

        var result = await _queryProcessor
            .RunQueryAsync(new StartPasskeyRegistrationQuery { UserId = CurrentUserId }, cancellationToken)
            .ConfigureAwait(false);
        if (result.IsNone)
        {
            return NotFound();
        }

        return Content(result.Get().OptionsJson, "application/json");
    }

    /// <summary>Verifies the new credential against the open registration challenge and stores it.</summary>
    [HttpPost("")]
    [EnableRateLimiting("sensitive")]
    public async Task<ActionResult<PasskeyResponse>> RegisterAsync(
        [FromBody] RegisterPasskeyDto requestDto,
        CancellationToken cancellationToken
    )
    {
        if (!_webAuthnOptions.Enabled)
        {
            return NotFound();
        }

        if (!await HasRecentReauthenticationAsync(cancellationToken).ConfigureAwait(false))
        {
            return ReauthenticationRequired();
        }

        if (!TryReadName(requestDto?.Name, out var name))
        {
            return BadRequest(Error(SecondFactorErrorResponse.InvalidName));
        }

        if (requestDto!.AttestationResponse.ValueKind != JsonValueKind.Object)
        {
            return BadRequest(Error(SecondFactorErrorResponse.PasskeyRejected));
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new CompletePasskeyRegistrationQuery
                {
                    UserId = CurrentUserId,
                    Name = name,
                    AttestationResponseJson = requestDto.AttestationResponse.GetRawText(),
                },
                cancellationToken
            )
            .ConfigureAwait(false);
        if (result.IsNone)
        {
            return NotFound();
        }

        var registration = result.Get();
        return registration.Status switch
        {
            PasskeyRegistrationStatus.Registered => StatusCode(
                StatusCodes.Status201Created,
                ToResponse(registration.Passkey!)
            ),
            PasskeyRegistrationStatus.ChallengeExpired => BadRequest(Error(SecondFactorErrorResponse.ChallengeExpired)),
            PasskeyRegistrationStatus.AlreadyRegistered => Conflict(
                Error(SecondFactorErrorResponse.PasskeyAlreadyRegistered)
            ),
            _ => BadRequest(Error(SecondFactorErrorResponse.PasskeyRejected)),
        };
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<PasskeyResponse>> RenameAsync(
        Guid id,
        [FromBody] RenamePasskeyDto requestDto,
        CancellationToken cancellationToken
    )
    {
        if (!_webAuthnOptions.Enabled)
        {
            return NotFound();
        }

        if (!TryReadName(requestDto?.Name, out var name))
        {
            return BadRequest(Error(SecondFactorErrorResponse.InvalidName));
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new RenamePasskeyQuery
                {
                    UserId = CurrentUserId,
                    PasskeyId = id,
                    Name = name,
                },
                cancellationToken
            )
            .ConfigureAwait(false);
        if (result.IsNone)
        {
            return NotFound();
        }

        return Ok(ToResponse(result.Get()));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!_webAuthnOptions.Enabled)
        {
            return NotFound();
        }

        if (!await HasRecentReauthenticationAsync(cancellationToken).ConfigureAwait(false))
        {
            return ReauthenticationRequired();
        }

        var result = await _queryProcessor
            .RunQueryAsync(new DeletePasskeyQuery { UserId = CurrentUserId, PasskeyId = id }, cancellationToken)
            .ConfigureAwait(false);
        if (result.IsNone)
        {
            return NotFound();
        }

        return result.Get().Status == PasskeyDeletionStatus.LastSecondFactor
            ? Conflict(Error(SecondFactorErrorResponse.LastSecondFactor))
            : NoContent();
    }

    private static SecondFactorErrorResponse Error(string error) => new() { Error = error };

    private static bool TryReadName(string? value, out string name)
    {
        name = value?.Trim() ?? string.Empty;
        return name.Length is > 0 and <= MaxNameLength;
    }

    private static PasskeyResponse ToResponse(PasskeySummary passkey) =>
        new()
        {
            Id = passkey.Id,
            Name = passkey.Name,
            CreatedAt = passkey.CreatedAt,
            LastUsedAt = passkey.LastUsedAt,
            IsBackedUp = passkey.IsBackedUp,
        };

    private ObjectResult ReauthenticationRequired() =>
        StatusCode(StatusCodes.Status403Forbidden, Error(SecondFactorErrorResponse.ReauthenticationRequired));

    private Task<bool> HasRecentReauthenticationAsync(CancellationToken cancellationToken) =>
        RecentReauthentication.HasAsync(_queryProcessor, User, CurrentUserId, cancellationToken);
}
