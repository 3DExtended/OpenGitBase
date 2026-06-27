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
[Authorize]
[Route("repository/{repositoryId:guid}/protected-branch-rules")]
public class RepositoryProtectedBranchRulesController : ControllerBase
{
    private readonly IQueryProcessor _queryProcessor;
    private readonly IUserContext _userContext;

    public RepositoryProtectedBranchRulesController(
        IQueryProcessor queryProcessor,
        IUserContext userContext
    )
    {
        _queryProcessor = queryProcessor;
        _userContext = userContext;
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid repositoryId, CancellationToken cancellationToken)
    {
        var repository = await LoadRepositoryAsync(repositoryId, cancellationToken).ConfigureAwait(false);
        if (repository is null)
        {
            return NotFound();
        }

        if (!await CanManageAsync(repository, cancellationToken).ConfigureAwait(false))
        {
            return Forbid();
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new ListProtectedBranchRulesQuery { RepositoryId = repository.Id },
                cancellationToken
            )
            .ConfigureAwait(false);
        return result.IsSome ? Ok(result.Get()) : NotFound();
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid repositoryId, Guid id, CancellationToken cancellationToken)
    {
        var repository = await LoadRepositoryAsync(repositoryId, cancellationToken).ConfigureAwait(false);
        if (repository is null)
        {
            return NotFound();
        }

        if (!await CanManageAsync(repository, cancellationToken).ConfigureAwait(false))
        {
            return Forbid();
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new GetProtectedBranchRuleQuery { ModelId = ProtectedBranchRuleId.From(id) },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (result.IsNone || result.Get().RepositoryId != repository.Id)
        {
            return NotFound();
        }

        return Ok(result.Get());
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        Guid repositoryId,
        [FromBody] UpsertProtectedBranchRuleRequest request,
        CancellationToken cancellationToken
    )
    {
        var repository = await LoadRepositoryAsync(repositoryId, cancellationToken).ConfigureAwait(false);
        if (repository is null)
        {
            return NotFound();
        }

        if (!await CanManageAsync(repository, cancellationToken).ConfigureAwait(false))
        {
            return Forbid();
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new CreateProtectedBranchRuleQuery
                {
                    ModelToCreate = ToModel(repository.Id, request),
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (result.IsNone)
        {
            return BadRequest();
        }

        var createdId = result.Get();
        return CreatedAtAction(
            nameof(Get),
            new { repositoryId, id = createdId.Value },
            createdId
        );
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid repositoryId,
        Guid id,
        [FromBody] UpsertProtectedBranchRuleRequest request,
        CancellationToken cancellationToken
    )
    {
        var repository = await LoadRepositoryAsync(repositoryId, cancellationToken).ConfigureAwait(false);
        if (repository is null)
        {
            return NotFound();
        }

        if (!await CanManageAsync(repository, cancellationToken).ConfigureAwait(false))
        {
            return Forbid();
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new UpdateProtectedBranchRuleQuery
                {
                    UpdatedModel = ToModel(repository.Id, request, ProtectedBranchRuleId.From(id)),
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsSome ? NoContent() : NotFound();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid repositoryId,
        Guid id,
        CancellationToken cancellationToken
    )
    {
        var repository = await LoadRepositoryAsync(repositoryId, cancellationToken).ConfigureAwait(false);
        if (repository is null)
        {
            return NotFound();
        }

        if (!await CanManageAsync(repository, cancellationToken).ConfigureAwait(false))
        {
            return Forbid();
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new DeleteProtectedBranchRuleQuery { Id = ProtectedBranchRuleId.From(id) },
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsSome ? NoContent() : NotFound();
    }

    private static ProtectedBranchRuleDto ToModel(
        RepositoryId repositoryId,
        UpsertProtectedBranchRuleRequest request,
        ProtectedBranchRuleId? id = null
    )
    {
        var normalizedPattern = RepositoryBranchPatternMatcher.ResolvePattern(
            request.Pattern,
            DefaultRefResolver.DefaultBranchPatternAlias
        );

        return new ProtectedBranchRuleDto
        {
            Id = id ?? ProtectedBranchRuleId.From(Guid.Empty),
            RepositoryId = repositoryId,
            Pattern = normalizedPattern,
            BlockDirectPush = request.BlockDirectPush,
            AllowedPushRoles = request.AllowedPushRoles,
            AllowedPushUserIds = request.AllowedPushUserIds.Select(UserId.From).ToList(),
            RequireMergeRequest = request.RequireMergeRequest,
            RequiredApprovalCount = request.RequiredApprovalCount,
            MergeRoleThreshold = (int)request.MergeRoleThreshold,
            ForcePushPolicy = request.ForcePushPolicy,
            DismissApprovalsOnPush = request.DismissApprovalsOnPush,
            LockedMergeStrategy = request.LockedMergeStrategy,
            PushRules = request
                .PushRules.Select(rule => new PushRuleDto
                {
                    Id = PushRuleId.From(rule.Id ?? Guid.Empty),
                    RuleType = rule.RuleType,
                    ConfigJson = rule.ConfigJson,
                })
                .ToList(),
        };
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
