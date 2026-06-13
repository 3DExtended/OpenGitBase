using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    public RepositoryMemberController(IQueryProcessor queryProcessor, IUserContext userContext)
    {
        _queryProcessor = queryProcessor;
        _userContext = userContext;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateRepositoryMemberQuery query,
        CancellationToken cancellationToken
    )
    {
        // get repository if it exists
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
            // now check if user has permissions to add members to the repository
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

        // first check if there is an existing repository member with the given id, if not return 404
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
            // if the id of the existing repository member doesn't match the id in the route, return 404
            return NotFound();
        }

        // get repository if it exists
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
            // now check if user has permissions to add members to the repository
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

        // first check if there is an existing repository member with the given id, if not return 404
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

        // get repository if it exists
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
            // now check if user has permissions to add members to the repository
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

    private IActionResult ToActionResult<T>(Option<T> result)
    {
        if (result.IsNone)
        {
            return NotFound();
        }

        return Ok(result.Get());
    }
}
