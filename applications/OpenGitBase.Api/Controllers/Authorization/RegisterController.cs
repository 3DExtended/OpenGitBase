using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using OpenGitBase.Api.Models;
using OpenGitBase.Common.Auth;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Contracts.Queries.Users;

namespace OpenGitBase.Api.Controllers.Authorization;

[ApiController]
[Route("register")]
[AllowAnonymous]
public class RegisterController : ControllerBase
{
    private readonly IMemoryCache _cache;
    private readonly IQueryProcessor _queryProcessor;
    private readonly IJWTTokenGenerator _jwtTokenGenerator;
    private readonly IEmailProtectionService _emailProtectionService;

    public RegisterController(
        IMemoryCache cache,
        IQueryProcessor queryProcessor,
        IJWTTokenGenerator jwtTokenGenerator,
        IEmailProtectionService emailProtectionService
    )
    {
        _cache = cache;
        _queryProcessor = queryProcessor;
        _jwtTokenGenerator = jwtTokenGenerator;
        _emailProtectionService = emailProtectionService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<string>> RegisterAsync(
        [FromBody] RegisterDto registerDto,
        CancellationToken cancellationToken
    )
    {
        if (
            registerDto == null
            || string.IsNullOrWhiteSpace(registerDto.Username)
            || string.IsNullOrWhiteSpace(registerDto.Email)
            || string.IsNullOrWhiteSpace(registerDto.Password)
        )
        {
            return BadRequest();
        }

        var usernameExists = await _queryProcessor
            .RunQueryAsync(
                new UserExistsByUsernameQuery { Username = registerDto.Username },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (usernameExists.IsNone || usernameExists.Get())
        {
            return Conflict("Username taken");
        }

        var emailExists = await _queryProcessor
            .RunQueryAsync(
                new UserExistsByEmailQuery { Email = registerDto.Email },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (emailExists.IsNone || emailExists.Get())
        {
            return Conflict("Email taken");
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new UserRegisterQuery
                {
                    Username = registerDto.Username,
                    Email = registerDto.Email,
                    Password = registerDto.Password,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (result.IsNone)
        {
            return BadRequest();
        }

        var token = _jwtTokenGenerator.GetJWTToken(
            registerDto.Username,
            result.Get().Value.ToString()
        );
        return Ok(token);
    }

    [HttpPost("openprovider")]
    public async Task<ActionResult<string>> OpenProviderRegisterAsync(
        [FromBody] OpenProviderRegisterDto registerDto,
        CancellationToken cancellationToken
    )
    {
        if (
            registerDto == null
            || string.IsNullOrWhiteSpace(registerDto.Username)
            || string.IsNullOrWhiteSpace(registerDto.RegistrationToken)
        )
        {
            return BadRequest();
        }

        if (
            !_cache.TryGetValue(
                "registrationapikey" + registerDto.RegistrationToken,
                out Dictionary<string, string>? cachedRegistration
            )
            || cachedRegistration == null
            || !cachedRegistration.TryGetValue("sub", out var internalId)
            || !cachedRegistration.TryGetValue("email", out var email)
            || string.IsNullOrWhiteSpace(internalId)
            || string.IsNullOrWhiteSpace(email)
        )
        {
            return Unauthorized();
        }

        var usernameExists = await _queryProcessor
            .RunQueryAsync(
                new UserExistsByUsernameQuery { Username = registerDto.Username },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (usernameExists.IsNone || usernameExists.Get())
        {
            return Conflict("Username taken");
        }

        var emailExists = await _queryProcessor
            .RunQueryAsync(new UserExistsByEmailQuery { Email = email }, cancellationToken)
            .ConfigureAwait(false);

        if (emailExists.IsNone || emailExists.Get())
        {
            return Conflict("Email taken");
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new UserCreateQuery
                {
                    ModelToCreate = new User { Username = registerDto.Username },
                    UserCredentials = new UserCredentials
                    {
                        Username = registerDto.Username,
                        SignInProvider = true,
                        InternalId = internalId,
                        EmailCiphertext = _emailProtectionService.EncryptEmail(email),
                        EmailLookupHash = _emailProtectionService.ComputeLookupHash(email),
                    },
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (result.IsNone)
        {
            return BadRequest();
        }

        _cache.Remove("registrationapikey" + registerDto.RegistrationToken);

        var token = _jwtTokenGenerator.GetJWTToken(
            registerDto.Username,
            result.Get().Value.ToString()
        );
        return Ok(token);
    }
}
