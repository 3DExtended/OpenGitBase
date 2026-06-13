using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenGitBase.Common.Auth;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.PublicGitSshKey.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Api.Controllers;

[ApiController]
[Authorize]
[Route("public-git-ssh-key")]
public class PublicGitSshKeyController : ControllerBase
{
    private readonly IQueryProcessor _queryProcessor;
    private readonly IUserContext _userContext;

    private readonly ISshKeyService _sshKeyService;

    public PublicGitSshKeyController(
        IQueryProcessor queryProcessor,
        IUserContext userContext,
        ISshKeyService sshKeyService
    )
    {
        _queryProcessor = queryProcessor;
        _userContext = userContext;
        _sshKeyService = sshKeyService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreatePublicGitSshKeyQuery query,
        CancellationToken cancellationToken
    )
    {
        query.ModelToCreate.OwnerUserId = UserId.From(_userContext.User.UserId);

        // ensure that fingerprint is correctly calculated based on the provided public SSH key
        try
        {
            var fingerprint = _sshKeyService.ValidateAndGetFingerprint(
                query.ModelToCreate.PublicSSHKey
            );

            if (fingerprint == null)
            {
                return BadRequest(new { error = "Invalid SSH public key." });
            }

            if (fingerprint != query.ModelToCreate.Fingerprint)
            {
                return BadRequest(
                    new
                    {
                        error = "Provided fingerprint does not match the calculated fingerprint of the SSH key.",
                    }
                );
            }

            query.ModelToCreate.Fingerprint = fingerprint;
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        var result = await _queryProcessor.RunQueryAsync(query, cancellationToken);
        if (result.IsNone)
        {
            return NotFound();
        }

        var id = result.Get();
        return CreatedAtAction(nameof(Get), new { id = id.Value }, id);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await _queryProcessor.RunQueryAsync(
            new GetPublicGitSshKeyQuery { ModelId = PublicGitSshKeyId.From(id) },
            cancellationToken
        );

        if (result.IsNone || result.Get().OwnerUserId != UserId.From(_userContext.User.UserId))
        {
            return NotFound();
        }

        return ToActionResult(result);
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await _queryProcessor.RunQueryAsync(
            new ListPublicGitSshKeyQuery { ForUser = UserId.From(_userContext.User.UserId) },
            cancellationToken
        );
        return ToActionResult(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var getResult = await _queryProcessor.RunQueryAsync(
            new GetPublicGitSshKeyQuery { ModelId = PublicGitSshKeyId.From(id) },
            cancellationToken
        );

        if (
            getResult.IsNone
            || getResult.Get().OwnerUserId != UserId.From(_userContext.User.UserId)
        )
        {
            return NotFound();
        }

        var result = await _queryProcessor.RunQueryAsync(
            new DeletePublicGitSshKeyQuery { Id = PublicGitSshKeyId.From(id) },
            cancellationToken
        );

        if (result.IsNone)
        {
            return NotFound();
        }

        return NoContent();
    }

    private IActionResult ToActionResult<T>(Option<T> result)
    {
        if (result.IsNone)
        {
            return NotFound();
        }

        return Ok(result.Get());
    }
}
