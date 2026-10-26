using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenGitBase.Common.Auth;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Api.Controllers;

[ApiController]
[Authorize(Roles = "admin")]
[Route("admin/users")]
public sealed class AdminUsersController : ControllerBase
{
    private readonly IQueryProcessor _queryProcessor;
    private readonly IUserContext _userContext;

    public AdminUsersController(IQueryProcessor queryProcessor, IUserContext userContext)
    {
        _queryProcessor = queryProcessor;
        _userContext = userContext;
    }

    /// <summary>
    /// Removes the user's authenticator app, recovery codes and passkeys, turns 2FA off, revokes every
    /// session and emails the user. Works whether or not <c>TwoFactor:Enabled</c> is on.
    /// </summary>
    [HttpPost("{username}/two-factor/reset")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetTwoFactorAsync(string username, CancellationToken cancellationToken)
    {
        var result = await _queryProcessor.RunQueryAsync(
            new ResetTwoFactorQuery { Username = username, AdminUserId = UserId.From(_userContext.User.UserId) },
            cancellationToken
        );
        return result.IsSome ? NoContent() : NotFound();
    }
}
