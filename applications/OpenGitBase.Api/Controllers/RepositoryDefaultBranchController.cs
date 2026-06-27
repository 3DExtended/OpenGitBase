using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Auth;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.RepositoryMember.Contracts;

namespace OpenGitBase.Api.Controllers;

[ApiController]
[Authorize]
[Route("repository/{repositoryId:guid}/settings/default-branch")]
public sealed class RepositoryDefaultBranchController : ControllerBase
{
    private readonly IQueryProcessor _queryProcessor;
    private readonly IUserContext _userContext;
    private readonly RepositoryContentService _contentService;

    public RepositoryDefaultBranchController(
        IQueryProcessor queryProcessor,
        IUserContext userContext,
        RepositoryContentService contentService
    )
    {
        _queryProcessor = queryProcessor;
        _userContext = userContext;
        _contentService = contentService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(Guid repositoryId, CancellationToken cancellationToken)
    {
        var repository = await LoadRepositoryAsync(repositoryId, cancellationToken)
            .ConfigureAwait(false);
        if (repository is null)
        {
            return NotFound();
        }

        if (!await CanManageAsync(repository, cancellationToken).ConfigureAwait(false))
        {
            return Forbid();
        }

        return Ok(
            new RepositoryDefaultBranchResponse
            {
                DefaultBranchName = repository.DefaultBranchName,
            }
        );
    }

    [HttpPatch]
    public async Task<IActionResult> Update(
        Guid repositoryId,
        [FromBody] UpdateRepositoryDefaultBranchRequest request,
        CancellationToken cancellationToken
    )
    {
        var repository = await LoadRepositoryAsync(repositoryId, cancellationToken)
            .ConfigureAwait(false);
        if (repository is null)
        {
            return NotFound();
        }

        if (!await CanManageAsync(repository, cancellationToken).ConfigureAwait(false))
        {
            return Forbid();
        }

        var branchNames = await _contentService
            .ListBranchNamesAsync(repository, cancellationToken)
            .ConfigureAwait(false);

        var result = await _queryProcessor
            .RunQueryAsync(
                new UpdateRepositoryDefaultBranchQuery
                {
                    RepositoryId = repository.Id,
                    DefaultBranchName = request.DefaultBranchName,
                    KnownBranchNames = branchNames,
                    AllowMissingBranch = branchNames.Count == 0,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (result.IsNone)
        {
            return BadRequest(new { error = "Default branch must match an existing branch name." });
        }

        return Ok(
            new RepositoryDefaultBranchResponse
            {
                DefaultBranchName = result.Get().DefaultBranchName,
            }
        );
    }

    private async Task<RepositoryDto?> LoadRepositoryAsync(
        Guid repositoryId,
        CancellationToken cancellationToken
    )
    {
        var result = await _queryProcessor
            .RunQueryAsync(
                new GetRepositoryQuery { ModelId = RepositoryId.From(repositoryId) },
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsSome ? result.Get() : null;
    }

    private async Task<bool> CanManageAsync(
        RepositoryDto repository,
        CancellationToken cancellationToken
    )
    {
        var userId = _userContext.GetUserId();
        if (repository.OwnerUserId == userId)
        {
            return true;
        }

        var membership = await _queryProcessor
            .RunQueryAsync(
                new GetRepositoryMemberQuery
                {
                    RepositoryId = repository.Id,
                    UserId = userId,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return membership.IsSome && membership.Get().Role >= RepositoryRole.Admin;
    }
}
