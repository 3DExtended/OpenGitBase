using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenGitBase.Api.Models;
using OpenGitBase.Common.Auth;
using OpenGitBase.Common.Options;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.GitAccessToken.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/git")]
public sealed class GitConfigController : ControllerBase
{
    private readonly GitOptions _gitOptions;

    public GitConfigController(GitOptions gitOptions)
    {
        _gitOptions = gitOptions;
    }

    [HttpGet("config")]
    [ProducesResponseType(typeof(GitConfigResponse), StatusCodes.Status200OK)]
    public ActionResult<GitConfigResponse> GetConfig()
    {
        return Ok(
            new GitConfigResponse
            {
                GitBaseUrl = _gitOptions.PublicBaseUrl.TrimEnd('/'),
                SshEnabled = _gitOptions.SshEnabled,
            }
        );
    }
}
