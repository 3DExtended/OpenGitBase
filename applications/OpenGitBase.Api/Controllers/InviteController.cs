using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenGitBase.Common.Auth;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Contracts;

namespace OpenGitBase.Api.Controllers;

[ApiController]
[Route("invite")]
public class InviteController : ControllerBase
{
    private readonly IQueryProcessor _queryProcessor;
    private readonly IUserContext _userContext;

    public InviteController(IQueryProcessor queryProcessor, IUserContext userContext)
    {
        _queryProcessor = queryProcessor;
        _userContext = userContext;
    }

    [HttpGet("{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetByToken(string token, CancellationToken cancellationToken)
    {
        var result = await _queryProcessor
            .RunQueryAsync(new GetOrganizationInviteByTokenQuery { Token = token }, cancellationToken)
            .ConfigureAwait(false);
        return result.IsSome ? Ok(result.Get()) : NotFound();
    }

    [HttpPost("{token}/accept")]
    [Authorize]
    public async Task<IActionResult> Accept(string token, CancellationToken cancellationToken)
    {
        var result = await _queryProcessor
            .RunQueryAsync(
                new AcceptOrganizationInviteQuery
                {
                    Token = token,
                    UserId = _userContext.GetUserId(),
                },
                cancellationToken
            )
            .ConfigureAwait(false);
        if (result.IsNone)
        {
            return NotFound();
        }

        return result.Get() switch
        {
            AcceptOrganizationInviteResult.Accepted => NoContent(),
            AcceptOrganizationInviteResult.NotFound => NotFound(),
            AcceptOrganizationInviteResult.Expired => BadRequest(new { error = "Invite has expired." }),
            AcceptOrganizationInviteResult.EmailMismatch => BadRequest(
                new { error = "Invite email does not match your account email." }
            ),
            AcceptOrganizationInviteResult.AlreadyMember => Conflict(
                new { error = "You are already a member of this organization." }
            ),
            _ => BadRequest(),
        };
    }

    [HttpPost("{token}/decline")]
    [AllowAnonymous]
    public async Task<IActionResult> Decline(string token, CancellationToken cancellationToken)
    {
        var result = await _queryProcessor
            .RunQueryAsync(new DeclineOrganizationInviteQuery { Token = token }, cancellationToken)
            .ConfigureAwait(false);
        return result.IsSome ? NoContent() : NotFound();
    }
}
