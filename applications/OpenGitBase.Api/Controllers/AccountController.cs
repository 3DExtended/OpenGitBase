using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenGitBase.Api.Models;
using OpenGitBase.Common.Auth;
using OpenGitBase.Common.Options;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Contracts.Queries.Users;

namespace OpenGitBase.Api.Controllers;

[ApiController]
[Route("account")]
[Authorize]
public class AccountController : ControllerBase
{
    private readonly IQueryProcessor _queryProcessor;
    private readonly IUserContext _userContext;
    private readonly DebugFeaturesOptions _debugFeatures;

    public AccountController(
        IQueryProcessor queryProcessor,
        IUserContext userContext,
        DebugFeaturesOptions debugFeatures
    )
    {
        _queryProcessor = queryProcessor;
        _userContext = userContext;
        _debugFeatures = debugFeatures;
    }

    [HttpGet("me")]
    public async Task<ActionResult<AccountMeResponse>> MeAsync(CancellationToken cancellationToken)
    {
        var userId = UserId.From(_userContext.User.UserId);
        var user = await _queryProcessor
            .RunQueryAsync(new UserGetByIdQuery { ModelId = userId }, cancellationToken)
            .ConfigureAwait(false);
        var emailVerified = await _queryProcessor
            .RunQueryAsync(new UserGetEmailVerifiedQuery { UserId = userId }, cancellationToken)
            .ConfigureAwait(false);

        if (user.IsNone || emailVerified.IsNone)
        {
            return NotFound();
        }

        var userModel = user.Get();
        var response = new AccountMeResponse
        {
            UserId = userId.Value,
            Username = userModel.Username,
            EmailVerified = emailVerified.Get(),
            IsAdmin = userModel.IsAdmin,
        };

        if (_debugFeatures.Features.EmailVerification)
        {
            response.Debug = new AccountDebugFeaturesDto { EmailVerification = true };
        }

        return Ok(response);
    }

    [HttpPost("verify-email")]
    [AllowAnonymous]
    public async Task<ActionResult<string>> VerifyEmailAsync(
        [FromBody] VerifyEmailDto requestDto,
        CancellationToken cancellationToken
    )
    {
        if (
            requestDto == null
            || string.IsNullOrWhiteSpace(requestDto.Username)
            || string.IsNullOrWhiteSpace(requestDto.VerificationToken)
        )
        {
            return BadRequest();
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new UserVerifyEmailQuery
                {
                    Username = requestDto.Username,
                    VerificationToken = requestDto.VerificationToken,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsSome ? Ok("Email verified") : BadRequest("Could not verify email!");
    }

    [HttpPost("resend-verification")]
    public async Task<ActionResult<string>> ResendVerificationAsync(
        CancellationToken cancellationToken
    )
    {
        var result = await _queryProcessor
            .RunQueryAsync(
                new UserResendVerificationEmailQuery
                {
                    UserId = UserId.From(_userContext.User.UserId),
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsSome
            ? Ok("Verification email sent")
            : BadRequest("Could not send verification email!");
    }

    [HttpPost("debug/verify-email")]
    public async Task<ActionResult<string>> DebugVerifyEmailAsync(
        CancellationToken cancellationToken
    )
    {
        if (!_debugFeatures.Features.EmailVerification)
        {
            return NotFound();
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new UserDebugVerifyEmailQuery { UserId = UserId.From(_userContext.User.UserId) },
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsSome ? Ok("Email verified") : StatusCode(StatusCodes.Status403Forbidden);
    }

    [HttpPost("debug/verification-code")]
    public async Task<ActionResult<DebugVerificationCodeResponse>> DebugVerificationCodeAsync(
        CancellationToken cancellationToken
    )
    {
        if (!_debugFeatures.Features.EmailVerification)
        {
            return NotFound();
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new UserDebugGenerateVerificationCodeQuery
                {
                    UserId = UserId.From(_userContext.User.UserId),
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (result.IsNone)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var code = result.Get();
        return Ok(
            new DebugVerificationCodeResponse { Code = code.Code, ExpiresAt = code.ExpiresAt }
        );
    }

    [HttpPost("change-password")]
    public async Task<ActionResult<string>> ChangePasswordAsync(
        [FromBody] ChangePasswordDto requestDto,
        CancellationToken cancellationToken
    )
    {
        if (
            requestDto == null
            || string.IsNullOrWhiteSpace(requestDto.CurrentPassword)
            || string.IsNullOrWhiteSpace(requestDto.NewPassword)
        )
        {
            return BadRequest();
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new UserChangePasswordQuery
                {
                    UserId = UserId.From(_userContext.User.UserId),
                    CurrentPassword = requestDto.CurrentPassword,
                    NewPassword = requestDto.NewPassword,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsSome ? Ok("Password changed") : BadRequest("Could not change password!");
    }

    [HttpPost("delete")]
    public async Task<ActionResult<UserDeleteAccountResult>> DeleteAccountAsync(
        [FromBody] DeleteAccountDto requestDto,
        CancellationToken cancellationToken
    )
    {
        if (requestDto == null || string.IsNullOrWhiteSpace(requestDto.Password))
        {
            return BadRequest();
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new UserDeleteAccountQuery
                {
                    UserId = UserId.From(_userContext.User.UserId),
                    Password = requestDto.Password,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (result.IsNone)
        {
            return BadRequest("Could not delete account!");
        }

        var deleteResult = result.Get();
        if (!deleteResult.Success)
        {
            return Conflict(deleteResult);
        }

        return Ok(deleteResult);
    }
}
