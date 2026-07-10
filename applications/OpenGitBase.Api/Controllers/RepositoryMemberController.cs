using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Auth;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Api.Controllers;

[ApiController]
[Route("repository-member")]
[Authorize]
public class RepositoryMemberController : ControllerBase
{
    private readonly IQueryProcessor _queryProcessor;
    private readonly IUserContext _userContext;
    private readonly RepositoryContentAuthorizationService _authorization;

    public RepositoryMemberController(
        IQueryProcessor queryProcessor,
        IUserContext userContext,
        RepositoryContentAuthorizationService authorization
    )
    {
        _queryProcessor = queryProcessor;
        _userContext = userContext;
        _authorization = authorization;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateRepositoryMemberQuery query,
        CancellationToken cancellationToken
    )
    {
        var repository = await _queryProcessor.RunQueryAsync(
            new GetRepositoryQuery { ModelId = query.ModelToCreate.RepositoryId },
            cancellationToken
        );

        if (repository.IsNone)
        {
            return NotFound();
        }

        if (repository.Get().OwnerUserId != _userContext.GetUserId())
        {
            var repositoryMember = await _queryProcessor.RunQueryAsync(
                new GetRepositoryMemberQuery
                {
                    RepositoryId = query.ModelToCreate.RepositoryId,
                    UserId = _userContext.GetUserId(),
                },
                cancellationToken
            );

            if (repositoryMember.IsNone || repositoryMember.Get().Role < RepositoryRole.Admin)
            {
                return Forbid("Only repository owner or admins can add members to the repository.");
            }
        }

        var callerRole = await ResolveCallerEffectiveRoleAsync(
            repository.Get(),
            cancellationToken
        ).ConfigureAwait(false);
        if (callerRole is null)
        {
            return Forbid();
        }

        if (query.ModelToCreate.Role > callerRole)
        {
            return Forbid("Cannot grant a role higher than your own.");
        }

        var result = await _queryProcessor.RunQueryAsync(query, cancellationToken);
        if (result.IsNone)
        {
            return NotFound();
        }

        var id = result.Get();
        return CreatedAtAction("Create", new { id = id.Value }, id);
    }

    [HttpGet("{repositoryid:guid}")]
    public async Task<IActionResult> List(
        [FromRoute] Guid repositoryId,
        CancellationToken cancellationToken
    )
    {
        var access = await _authorization
            .AuthorizeReadByIdAsync(RepositoryId.From(repositoryId), cancellationToken)
            .ConfigureAwait(false);
        if (access.Kind != RepositoryContentAccessResultKind.Allowed)
        {
            return MapAccessFailure(access);
        }

        var result = await _queryProcessor.RunQueryAsync(
            new ListRepositoryMemberQuery { RepositoryId = RepositoryId.From(repositoryId) },
            cancellationToken
        );
        return ToActionResult(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateRepositoryMemberQuery query,
        CancellationToken cancellationToken
    )
    {
        query.UpdatedModel.Id = RepositoryMemberId.From(id);

        var existingRepositoryMember = await _queryProcessor
            .RunQueryAsync(
                new GetRepositoryMemberQuery
                {
                    RepositoryId = query.UpdatedModel.RepositoryId,
                    UserId = query.UpdatedModel.UserId,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (existingRepositoryMember.IsNone)
        {
            return NotFound();
        }

        if (existingRepositoryMember.Get().Id != query.UpdatedModel.Id)
        {
            return NotFound();
        }

        var repository = await _queryProcessor.RunQueryAsync(
            new GetRepositoryQuery { ModelId = query.UpdatedModel.RepositoryId },
            cancellationToken
        );

        if (repository.IsNone)
        {
            return NotFound();
        }

        if (repository.Get().OwnerUserId != _userContext.GetUserId())
        {
            var repositoryMember = await _queryProcessor.RunQueryAsync(
                new GetRepositoryMemberQuery
                {
                    RepositoryId = query.UpdatedModel.RepositoryId,
                    UserId = _userContext.GetUserId(),
                },
                cancellationToken
            );

            if (repositoryMember.IsNone || repositoryMember.Get().Role < RepositoryRole.Admin)
            {
                return Forbid("Only repository owner or admins can add members to the repository.");
            }
        }

        var callerRole = await ResolveCallerEffectiveRoleAsync(
            repository.Get(),
            cancellationToken
        ).ConfigureAwait(false);
        if (callerRole is null)
        {
            return Forbid();
        }

        if (query.UpdatedModel.Role > callerRole)
        {
            return Forbid("Cannot grant a role higher than your own.");
        }

        var result = await _queryProcessor.RunQueryAsync(query, cancellationToken);
        if (result.IsNone)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var repositoryMemberId = RepositoryMemberId.From(id);

        var existingRepositoryMember = await _queryProcessor
            .RunQueryAsync(
                new GetRepositoryMemberByIdQuery { ModelId = repositoryMemberId },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (existingRepositoryMember.IsNone)
        {
            return NotFound();
        }

        var repository = await _queryProcessor.RunQueryAsync(
            new GetRepositoryQuery { ModelId = existingRepositoryMember.Get().RepositoryId },
            cancellationToken
        );

        if (repository.IsNone)
        {
            return NotFound();
        }

        if (repository.Get().OwnerUserId != _userContext.GetUserId())
        {
            var repositoryMember = await _queryProcessor.RunQueryAsync(
                new GetRepositoryMemberQuery
                {
                    RepositoryId = existingRepositoryMember.Get().RepositoryId,
                    UserId = _userContext.GetUserId(),
                },
                cancellationToken
            );

            if (repositoryMember.IsNone || repositoryMember.Get().Role < RepositoryRole.Admin)
            {
                return Forbid("Only repository owner or admins can add members to the repository.");
            }
        }

        var result = await _queryProcessor.RunQueryAsync(
            new DeleteRepositoryMemberQuery { Id = repositoryMemberId },
            cancellationToken
        );

        if (result.IsNone)
        {
            return NotFound();
        }

        return NoContent();
    }

    private async Task<RepositoryRole?> ResolveCallerEffectiveRoleAsync(
        RepositoryDto repository,
        CancellationToken cancellationToken
    )
    {
        var callerId = _userContext.GetUserId();
        if (repository.OwnerUserId == callerId)
        {
            return RepositoryRole.Owner;
        }

        var membership = await _queryProcessor
            .RunQueryAsync(
                new GetRepositoryMemberQuery
                {
                    RepositoryId = repository.Id,
                    UserId = callerId,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return membership.IsSome ? membership.Get().Role : null;
    }

    private IActionResult ToActionResult<T>(Option<T> result)
    {
        if (result.IsNone)
        {
            return NotFound();
        }

        return Ok(result.Get());
    }

    private IActionResult MapAccessFailure(RepositoryContentAccessResult access) =>
        access.Kind switch
        {
            RepositoryContentAccessResultKind.NotFound => NotFound(),
            RepositoryContentAccessResultKind.Forbidden => Forbid(),
            RepositoryContentAccessResultKind.Unavailable => StatusCode(
                StatusCodes.Status503ServiceUnavailable
            ),
            _ => NotFound(),
        };
}
