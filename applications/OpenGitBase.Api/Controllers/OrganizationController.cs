using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Auth;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Contracts.Queries.Users;

namespace OpenGitBase.Api.Controllers;

[ApiController]
[Route("organization")]
[Authorize]
public class OrganizationController : ControllerBase
{
    private readonly IQueryProcessor _queryProcessor;
    private readonly IUserContext _userContext;
    private readonly IOrganizationAccessService _organizationAccess;

    public OrganizationController(
        IQueryProcessor queryProcessor,
        IUserContext userContext,
        IOrganizationAccessService organizationAccess
    )
    {
        _queryProcessor = queryProcessor;
        _userContext = userContext;
        _organizationAccess = organizationAccess;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateOrganizationQuery query,
        CancellationToken cancellationToken
    )
    {
        if (
            query?.ModelToCreate == null
            || string.IsNullOrWhiteSpace(query.ModelToCreate.Name)
            || string.IsNullOrWhiteSpace(query.ModelToCreate.Slug)
        )
        {
            return BadRequest();
        }

        if (ReservedSlugValidator.IsReserved(query.ModelToCreate.Slug))
        {
            return Conflict("Reserved organization slug");
        }

        var emailVerified = await _queryProcessor
            .RunQueryAsync(
                new UserGetEmailVerifiedQuery { UserId = UserId.From(_userContext.User.UserId) },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (emailVerified.IsNone || !emailVerified.Get())
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { error = "Email must be verified before creating organizations." }
            );
        }

        query.CreatorUserId = _userContext.User.UserId;
        query.ModelToCreate.OwnerUserId = _userContext.User.UserId;

        var result = await _queryProcessor
            .RunQueryAsync(query, cancellationToken)
            .ConfigureAwait(false);
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
        var result = await _queryProcessor
            .RunQueryAsync(
                new GetOrganizationQuery { ModelId = OrganizationId.From(id) },
                cancellationToken
            )
            .ConfigureAwait(false);
        return ToActionResult(result);
    }

    [HttpGet("by-slug/{slug}")]
    public async Task<IActionResult> GetBySlug(string slug, CancellationToken cancellationToken)
    {
        var result = await _queryProcessor
            .RunQueryAsync(new GetOrganizationBySlugQuery { Slug = slug }, cancellationToken)
            .ConfigureAwait(false);
        return ToActionResult(result);
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await _queryProcessor
            .RunQueryAsync(
                new ListUserOrganizationsQuery { UserId = UserId.From(_userContext.User.UserId) },
                cancellationToken
            )
            .ConfigureAwait(false);
        return ToActionResult(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateOrganizationQuery query,
        CancellationToken cancellationToken
    )
    {
        if (query?.UpdatedModel == null || string.IsNullOrWhiteSpace(query.UpdatedModel.Name))
        {
            return BadRequest();
        }

        var access = await _organizationAccess
            .CheckOwnerAccessAsync(
                OrganizationId.From(id),
                _userContext.GetUserId(),
                cancellationToken
            )
            .ConfigureAwait(false);

        if (!access.OrganizationExists)
        {
            return NotFound();
        }

        if (!access.IsOwner)
        {
            return Forbid();
        }

        var existing = access.Organization!;
        var updateQuery = new UpdateOrganizationQuery
        {
            UpdatedModel = new OrganizationDto
            {
                Id = OrganizationId.From(id),
                Name = query.UpdatedModel.Name,
                Slug = existing.Slug,
                OwnerUserId = existing.OwnerUserId,
            },
        };

        var result = await _queryProcessor
            .RunQueryAsync(updateQuery, cancellationToken)
            .ConfigureAwait(false);
        if (result.IsNone)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var organizationId = OrganizationId.From(id);
        var access = await _organizationAccess
            .CheckOwnerAccessAsync(organizationId, _userContext.GetUserId(), cancellationToken)
            .ConfigureAwait(false);

        if (!access.OrganizationExists)
        {
            return NotFound();
        }

        if (!access.IsOwner)
        {
            return Forbid();
        }

        var blockers = await _organizationAccess
            .GetDeleteBlockersAsync(organizationId, cancellationToken)
            .ConfigureAwait(false);

        if (blockers.Count > 0)
        {
            return Conflict(
                new OrganizationDeleteBlockedResult
                {
                    Success = false,
                    Blockers = blockers.ToList(),
                }
            );
        }

        var result = await _queryProcessor
            .RunQueryAsync(new DeleteOrganizationQuery { Id = organizationId }, cancellationToken)
            .ConfigureAwait(false);

        if (result.IsNone)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpGet("{id:guid}/members")]
    public async Task<IActionResult> ListMembers(Guid id, CancellationToken cancellationToken)
    {
        var result = await _queryProcessor
            .RunQueryAsync(
                new ListOrganizationMembersQuery { OrganizationId = OrganizationId.From(id) },
                cancellationToken
            )
            .ConfigureAwait(false);
        return ToActionResult(result);
    }

    [HttpPost("{id:guid}/members")]
    public async Task<IActionResult> AddMember(
        Guid id,
        [FromBody] AddOrganizationMemberRequest request,
        CancellationToken cancellationToken
    )
    {
        if (request == null)
        {
            return BadRequest();
        }

        var access = await _organizationAccess
            .CheckOwnerAccessAsync(
                OrganizationId.From(id),
                _userContext.GetUserId(),
                cancellationToken
            )
            .ConfigureAwait(false);

        if (!access.OrganizationExists)
        {
            return NotFound();
        }

        if (!access.IsOwner)
        {
            return Forbid();
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new AddOrganizationMemberQuery
                {
                    OrganizationId = OrganizationId.From(id),
                    UserId = UserId.From(request.UserId),
                    Role = request.Role,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsSome ? NoContent() : NotFound();
    }

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken
    )
    {
        var organizationId = OrganizationId.From(id);
        var access = await _organizationAccess
            .CheckOwnerAccessAsync(organizationId, _userContext.GetUserId(), cancellationToken)
            .ConfigureAwait(false);

        if (!access.OrganizationExists)
        {
            return NotFound();
        }

        if (!access.IsOwner)
        {
            return Forbid();
        }

        if (
            await _organizationAccess
                .WouldRemoveLastOwnerAsync(organizationId, UserId.From(userId), cancellationToken)
                .ConfigureAwait(false)
        )
        {
            return Conflict(new { error = "Cannot remove the last organization owner." });
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new RemoveOrganizationMemberQuery
                {
                    OrganizationId = organizationId,
                    UserId = UserId.From(userId),
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsSome ? NoContent() : NotFound();
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
