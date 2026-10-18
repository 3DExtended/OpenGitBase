using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Auth;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Contracts.Queries.Users;

namespace OpenGitBase.Api.Controllers.Authorization;

[ApiController]
[Route("signin")]
public class SignInController : ControllerBase
{
    private readonly IAppleIdentityTokenValidator _appleTokenValidator;
    private readonly IMemoryCache _cache;
    private readonly IGoogleIdentityTokenValidator _googleTokenValidator;
    private readonly IQueryProcessor _queryProcessor;
    private readonly IJWTTokenGenerator _jwtTokenGenerator;
    private readonly IAuthCookieService _authCookieService;
    private readonly SessionIssuer _sessionIssuer;

    public SignInController(
        IQueryProcessor queryProcessor,
        IAppleIdentityTokenValidator appleTokenValidator,
        IMemoryCache cache,
        IGoogleIdentityTokenValidator googleTokenValidator,
        IJWTTokenGenerator jwtTokenGenerator,
        IAuthCookieService authCookieService,
        SessionIssuer sessionIssuer
    )
    {
        _queryProcessor = queryProcessor;
        _appleTokenValidator = appleTokenValidator;
        _cache = cache;
        _googleTokenValidator = googleTokenValidator;
        _jwtTokenGenerator = jwtTokenGenerator;
        _authCookieService = authCookieService;
        _sessionIssuer = sessionIssuer;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("sensitive")]
    public async Task<ActionResult<string>> LoginAsync(
        [FromBody] LoginDto loginDto,
        CancellationToken cancellationToken
    )
    {
        if (loginDto == null || string.IsNullOrWhiteSpace(loginDto.Username))
        {
            return BadRequest();
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new UserLoginQuery { Username = loginDto.Username, Password = loginDto.Password },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (result.IsNone)
        {
            return Unauthorized();
        }

        var challenge = await BeginSecondFactorAsync(result.Get(), cancellationToken).ConfigureAwait(false);
        if (challenge is not null)
        {
            return Ok(challenge);
        }

        var user = await _queryProcessor
            .RunQueryAsync(
                new UserGetByIdQuery { ModelId = result.Get() },
                cancellationToken
            )
            .ConfigureAwait(false);

        var isAdmin = user.IsSome && user.Get().IsAdmin;
        var token = _jwtTokenGenerator.GetJWTToken(
            loginDto.Username,
            result.Get().Value.ToString(),
            isAdmin
        );
        _authCookieService.SetAuthCookie(Response, token);
        return Ok(token);
    }

    /// <summary>
    /// The second sign-in step for accounts with 2FA on: answers the challenge from <c>login</c>,
    /// <c>google</c> or <c>apple</c> with an authenticator code and returns the session token.
    /// </summary>
    [HttpPost("two-factor")]
    [AllowAnonymous]
    [EnableRateLimiting("sensitive")]
    public async Task<ActionResult<string>> CompleteTwoFactorAsync(
        [FromBody] TwoFactorSignInDto requestDto,
        CancellationToken cancellationToken
    )
    {
        if (
            requestDto == null
            || string.IsNullOrWhiteSpace(requestDto.ChallengeToken)
            || string.IsNullOrWhiteSpace(requestDto.Code)
        )
        {
            return BadRequest();
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new CompleteTwoFactorSignInQuery { ChallengeToken = requestDto.ChallengeToken, Code = requestDto.Code.Trim() },
                cancellationToken
            )
            .ConfigureAwait(false);
        var outcome = result.IsSome ? result.Get() : new TwoFactorSignInResult(TwoFactorSignInStatus.ChallengeExpired);

        switch (outcome.Status)
        {
            case TwoFactorSignInStatus.Succeeded:
                var token = await _sessionIssuer
                    .IssueAsync(Response, outcome.UserId!, cancellationToken)
                    .ConfigureAwait(false);
                return token is null
                    ? Unauthorized(new SecondFactorErrorResponse { Error = SecondFactorErrorResponse.ChallengeExpired })
                    : Ok(token);
            case TwoFactorSignInStatus.InvalidCode:
                return Unauthorized(new SecondFactorErrorResponse { Error = SecondFactorErrorResponse.InvalidCode });
            case TwoFactorSignInStatus.LockedOut:
                return StatusCode(
                    StatusCodes.Status429TooManyRequests,
                    new SecondFactorErrorResponse
                    {
                        Error = SecondFactorErrorResponse.LockedOut,
                        LockedUntil = outcome.LockedUntil,
                    }
                );
            default:
                return Unauthorized(new SecondFactorErrorResponse { Error = SecondFactorErrorResponse.ChallengeExpired });
        }
    }

    [HttpPost("signout")]
    [AllowAnonymous]
    public ActionResult SignOutAsync()
    {
        _authCookieService.ClearAuthCookie(Response);
        return Ok("Signed out");
    }

    [HttpPost("google")]
    [AllowAnonymous]
    [EnableRateLimiting("sensitive")]
    public async Task<ActionResult<string>> LoginWithGoogleAsync(
        [FromBody] [Required] GoogleLoginDto loginDto,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(loginDto.IdentityToken))
        {
            return BadRequest("Expected IdentityToken.");
        }

        try
        {
            await _googleTokenValidator
                .ValidateAsync(loginDto.IdentityToken, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (InvalidJwtException)
        {
            return Unauthorized();
        }

        var googleIdTokenDetails = GetTokenInfo(loginDto.IdentityToken);
        var internalId = "google" + googleIdTokenDetails["sub"];
        var email = googleIdTokenDetails.TryGetValue("email", out var emailValue)
            ? emailValue
            : string.Empty;

        return await HandleProviderLoginAsync(internalId, email, cancellationToken)
            .ConfigureAwait(false);
    }

    [HttpPost("apple")]
    [AllowAnonymous]
    [EnableRateLimiting("sensitive")]
    public async Task<ActionResult<string>> LoginWithAppleAsync(
        [FromBody] [Required] AppleLoginDto loginDto,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(loginDto.IdentityToken))
        {
            return BadRequest("Expected IdentityToken.");
        }

        var appleIdentity = await _appleTokenValidator
            .ValidateAsync(loginDto.IdentityToken, cancellationToken)
            .ConfigureAwait(false);

        if (appleIdentity is null)
        {
            return Unauthorized();
        }

        return await HandleProviderLoginAsync(
                appleIdentity.Subject,
                appleIdentity.Email,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    [HttpPost("requestresetpassword")]
    [AllowAnonymous]
    [EnableRateLimiting("sensitive")]
    public async Task<ActionResult<string>> RequestPasswordResetAsync(
        [FromBody] ResetPasswordRequestDto requestDto,
        CancellationToken cancellationToken
    )
    {
        if (
            requestDto == null
            || string.IsNullOrWhiteSpace(requestDto.Email)
            || string.IsNullOrWhiteSpace(requestDto.Username)
        )
        {
            return BadRequest();
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new UserRequestPasswordResetQuery
                {
                    Email = requestDto.Email,
                    Username = requestDto.Username,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsSome
            ? Ok("Email sent")
            : BadRequest("Could not create password reset mail or token!");
    }

    [HttpPost("resetpassword")]
    [AllowAnonymous]
    [EnableRateLimiting("sensitive")]
    public async Task<ActionResult<string>> UserPasswordResetAsync(
        [FromBody] ResetPasswordDto requestDto,
        CancellationToken cancellationToken
    )
    {
        if (
            requestDto == null
            || string.IsNullOrWhiteSpace(requestDto.Email)
            || string.IsNullOrWhiteSpace(requestDto.NewPassword)
            || string.IsNullOrWhiteSpace(requestDto.ResetCode)
            || string.IsNullOrWhiteSpace(requestDto.Username)
        )
        {
            return BadRequest();
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new UserPasswordResetQuery
                {
                    Email = requestDto.Email,
                    Username = requestDto.Username,
                    ResetCode = requestDto.ResetCode,
                    NewPassword = requestDto.NewPassword,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsSome ? Ok("New password set") : BadRequest("Could not set new password!");
    }

    [HttpGet("testlogin")]
    [Authorize]
    public ActionResult<string> TestLogin()
    {
        return Ok("ok");
    }

    private static Dictionary<string, string> GetTokenInfo(string token)
    {
        var tokenInfo = new Dictionary<string, string>();
        var handler = new JwtSecurityTokenHandler();
        var jwtSecurityToken = handler.ReadJwtToken(token);

        foreach (var claim in jwtSecurityToken.Claims)
        {
            tokenInfo[claim.Type] = claim.Value;
        }

        foreach (var header in jwtSecurityToken.Header)
        {
            tokenInfo[header.Key] = header.Value?.ToString() ?? string.Empty;
        }

        return tokenInfo;
    }

    /// <summary>A challenge in place of the session when the account has 2FA on; otherwise null.</summary>
    private async Task<TwoFactorChallengeResponse?> BeginSecondFactorAsync(
        UserId userId,
        CancellationToken cancellationToken
    )
    {
        var start = await _queryProcessor
            .RunQueryAsync(new BeginTwoFactorSignInQuery { UserId = userId }, cancellationToken)
            .ConfigureAwait(false);
        if (start.IsNone || !start.Get().Required)
        {
            return null;
        }

        return new TwoFactorChallengeResponse
        {
            ChallengeToken = start.Get().ChallengeToken ?? string.Empty,
            Methods = start.Get().Methods,
        };
    }

    private async Task<ActionResult<string>> HandleProviderLoginAsync(
        string internalId,
        string email,
        CancellationToken cancellationToken
    )
    {
        var possiblyExistingUserId = await _queryProcessor
            .RunQueryAsync(
                new UserGetByInternalIdQuery { InternalId = internalId },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (possiblyExistingUserId.IsSome)
        {
            var user = await _queryProcessor
                .RunQueryAsync(
                    new UserGetByIdQuery
                    {
                        ModelId = UserId.From(possiblyExistingUserId.Get().Value),
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);

            if (user.IsNone)
            {
                return BadRequest();
            }

            var challenge = await BeginSecondFactorAsync(possiblyExistingUserId.Get(), cancellationToken)
                .ConfigureAwait(false);
            if (challenge is not null)
            {
                return Ok(challenge);
            }

            var token = _jwtTokenGenerator.GetJWTToken(
                user.Get().Username,
                possiblyExistingUserId.Get().Value.ToString()
            );
            _authCookieService.SetAuthCookie(Response, token);
            return Ok(token);
        }

        var temporaryApiKey = Convert
            .ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('/', 'A')
            .Replace('+', 'b');

        _cache.GetOrCreate(
            "registrationapikey" + temporaryApiKey,
            entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2);
                return new Dictionary<string, string> { { "sub", internalId }, { "email", email } };
            }
        );

        return Ok("redirect" + temporaryApiKey);
    }
}
