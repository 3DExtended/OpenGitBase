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
        var organizationId = OrganizationId.From(id);
        var access = await _organizationAccess
            .CheckMemberAccessAsync(organizationId, _userContext.GetUserId(), cancellationToken)
            .ConfigureAwait(false);

        if (!access.OrganizationExists)
        {
            return NotFound();
        }

        if (!access.IsMember)
        {
            return Forbid();
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new ListOrganizationMembersQuery { OrganizationId = organizationId },
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

        if (string.IsNullOrWhiteSpace(request.Identifier))
        {
            return BadRequest();
        }

        var organizationId = OrganizationId.From(id);
        var resolvedUserId = await ResolveUserIdFromIdentifierAsync(
            request.Identifier,
            cancellationToken
        ).ConfigureAwait(false);

        if (resolvedUserId.IsNone)
        {
            return NotFound(new { error = "User not found." });
        }

        var userId = resolvedUserId.Get();
        var existingMember = await _queryProcessor
            .RunQueryAsync(
                new GetOrganizationMemberQuery
                {
                    OrganizationId = organizationId,
                    UserId = userId,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (existingMember.IsSome)
        {
            return Conflict(new { error = "User is already a member of this organization." });
        }

        var result = await _queryProcessor
            .RunQueryAsync(
                new AddOrganizationMemberQuery
                {
                    OrganizationId = organizationId,
                    UserId = userId,
                    Role = request.Role,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsSome ? NoContent() : NotFound();
    }

    [HttpPut("{id:guid}/members/{userId:guid}")]
    public async Task<IActionResult> UpdateMember(
        Guid id,
        Guid userId,
        [FromBody] UpdateOrganizationMemberRequest request,
        CancellationToken cancellationToken
    )
    {
        if (request == null)
        {
            return BadRequest();
        }

        var organizationId = OrganizationId.From(id);
        var memberUserId = UserId.From(userId);
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

        var existingMember = await _queryProcessor
            .RunQueryAsync(
                new GetOrganizationMemberQuery
                {
                    OrganizationId = organizationId,
                    UserId = memberUserId,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (existingMember.IsNone)
        {
            return NotFound();
        }

        if (
            request.Role == OrganizationMemberRole.Member
            && existingMember.Get().Role == OrganizationMemberRole.Owner
            && await _organizationAccess
                .WouldDemoteLastOwnerAsync(organizationId, memberUserId, cancellationToken)
                .ConfigureAwait(false)
        )
        {
            return Conflict(new { error = "Cannot demote the last organization owner." });
        }

        var member = existingMember.Get();
        var result = await _queryProcessor
            .RunQueryAsync(
                new UpdateOrganizationMemberQuery
                {
                    UpdatedModel = new OrganizationMemberDto
                    {
                        Id = member.Id,
                        OrganizationId = organizationId,
                        UserId = memberUserId,
                        Role = request.Role,
                    },
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
        var memberUserId = UserId.From(userId);
        var currentUserId = _userContext.GetUserId();
        var isSelf = currentUserId == memberUserId;

        if (isSelf)
        {
            var memberAccess = await _organizationAccess
                .CheckMemberAccessAsync(organizationId, currentUserId, cancellationToken)
                .ConfigureAwait(false);

            if (!memberAccess.OrganizationExists)
            {
                return NotFound();
            }

            if (!memberAccess.IsMember)
            {
                return Forbid();
            }
        }
        else
        {
            var ownerAccess = await _organizationAccess
                .CheckOwnerAccessAsync(organizationId, currentUserId, cancellationToken)
                .ConfigureAwait(false);

            if (!ownerAccess.OrganizationExists)
            {
                return NotFound();
            }

            if (!ownerAccess.IsOwner)
            {
                return Forbid();
            }
        }

        if (
            await _organizationAccess
                .WouldRemoveLastOwnerAsync(organizationId, memberUserId, cancellationToken)
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
                    UserId = memberUserId,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsSome ? NoContent() : NotFound();
    }

    private async Task<Option<UserId>> ResolveUserIdFromIdentifierAsync(
        string identifier,
        CancellationToken cancellationToken
    )
    {
        var trimmed = identifier.Trim();
        if (trimmed.Contains('@', StringComparison.Ordinal))
        {
            return await _queryProcessor
                .RunQueryAsync(
                    new UserGetIdByEmailQuery { Email = trimmed },
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        return await _queryProcessor
            .RunQueryAsync(
                new UserExistsByUsernameQuery { Username = trimmed },
                cancellationToken
            )
            .ConfigureAwait(false);
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
