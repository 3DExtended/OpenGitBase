using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Auth;
using OpenGitBase.Common.Options;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Contracts.Queries.Users;

namespace OpenGitBase.Api.Controllers.Authorization;

[ApiController]
[Route("signin")]
public class SignInController : ControllerBase
{
    private readonly AppleAuthOptions _appleAuthOptions;
    private readonly IMemoryCache _cache;
    private readonly IGoogleIdentityTokenValidator _googleTokenValidator;
    private readonly IQueryProcessor _queryProcessor;
    private readonly IJWTTokenGenerator _jwtTokenGenerator;
    private readonly IAuthCookieService _authCookieService;

    public SignInController(
        IQueryProcessor queryProcessor,
        AppleAuthOptions appleAuthOptions,
        IMemoryCache cache,
        IGoogleIdentityTokenValidator googleTokenValidator,
        IJWTTokenGenerator jwtTokenGenerator,
        IAuthCookieService authCookieService
    )
    {
        _queryProcessor = queryProcessor;
        _appleAuthOptions = appleAuthOptions;
        _cache = cache;
        _googleTokenValidator = googleTokenValidator;
        _jwtTokenGenerator = jwtTokenGenerator;
        _authCookieService = authCookieService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
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

        var token = _jwtTokenGenerator.GetJWTToken(
            loginDto.Username,
            result.Get().Value.ToString()
        );
        _authCookieService.SetAuthCookie(Response, token);
        return Ok(token);
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
    public async Task<ActionResult<string>> LoginWithAppleAsync(
        [FromBody] [Required] AppleLoginDto loginDto,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(loginDto.IdentityToken))
        {
            return BadRequest("Expected IdentityToken.");
        }

        var tokenDetails = GetTokenInfo(loginDto.IdentityToken);

        if (
            tokenDetails.TryGetValue("iss", out var issuer)
            && issuer != "https://appleid.apple.com"
        )
        {
            return Unauthorized();
        }

        if (
            tokenDetails.TryGetValue("aud", out var audience)
            && audience != _appleAuthOptions.ClientId
        )
        {
            return Unauthorized();
        }

        if (
            tokenDetails.TryGetValue("exp", out var expValue)
            && long.TryParse(expValue, out var expSeconds)
        )
        {
            var expDate = DateTimeOffset.FromUnixTimeSeconds(expSeconds);
            if (DateTimeOffset.UtcNow > expDate)
            {
                return Unauthorized();
            }
        }

        var internalId = tokenDetails["sub"];
        var email = tokenDetails.TryGetValue("email", out var emailValue)
            ? emailValue
            : string.Empty;

        return await HandleProviderLoginAsync(internalId, email, cancellationToken)
            .ConfigureAwait(false);
    }

    [HttpPost("requestresetpassword")]
    [AllowAnonymous]
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
