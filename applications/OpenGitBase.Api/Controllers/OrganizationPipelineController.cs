using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Auth;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Pipeline.Contracts;

namespace OpenGitBase.Api.Controllers;

[ApiController]
[Authorize]
[Route("organization/{organizationId:guid}/pipeline")]
public sealed class OrganizationPipelineController : ControllerBase
{
    private readonly IQueryProcessor _queryProcessor;
    private readonly IUserContext _userContext;
    private readonly IOrganizationAccessService _organizationAccess;

    public OrganizationPipelineController(
        IQueryProcessor queryProcessor,
        IUserContext userContext,
        IOrganizationAccessService organizationAccess
    )
    {
        _queryProcessor = queryProcessor;
        _userContext = userContext;
        _organizationAccess = organizationAccess;
    }

    [HttpGet("egress/domain-requests")]
    public async Task<IActionResult> ListDomainRequests(
        Guid organizationId,
        CancellationToken cancellationToken
    )
    {
        var access = await AuthorizeOwnerAsync(organizationId, cancellationToken).ConfigureAwait(false);
        if (access is not null)
        {
            return access;
        }

        var result = await _queryProcessor.RunQueryAsync(
            new ListDomainAllowanceRequestsQuery
            {
                Scope = DomainAllowanceRequestScope.Organization,
                OrganizationId = organizationId,
                Status = DomainAllowanceRequestStatus.Pending,
            },
            cancellationToken
        ).ConfigureAwait(false);
        return Ok(result.IsSome ? result.Get() : Array.Empty<DomainAllowanceRequestDto>());
    }

    [HttpPost("egress/domain-requests/{requestId:guid}/approve")]
    public Task<IActionResult> ApproveDomainRequest(
        Guid organizationId,
        Guid requestId,
        CancellationToken cancellationToken
    ) => ReviewDomainRequestAsync(organizationId, requestId, true, cancellationToken);

    [HttpPost("egress/domain-requests/{requestId:guid}/deny")]
    public Task<IActionResult> DenyDomainRequest(
        Guid organizationId,
        Guid requestId,
        CancellationToken cancellationToken
    ) => ReviewDomainRequestAsync(organizationId, requestId, false, cancellationToken);

    private async Task<IActionResult> ReviewDomainRequestAsync(
        Guid organizationId,
        Guid requestId,
        bool approve,
        CancellationToken cancellationToken
    )
    {
        var access = await AuthorizeOwnerAsync(organizationId, cancellationToken).ConfigureAwait(false);
        if (access is not null)
        {
            return access;
        }

        var result = await _queryProcessor.RunQueryAsync(
            new ReviewDomainAllowanceRequestQuery
            {
                RequestId = DomainAllowanceRequestId.From(requestId),
                Approve = approve,
                ReviewedByUserId = _userContext.User.UserId,
            },
            cancellationToken
        ).ConfigureAwait(false);
        return result.IsSome ? Ok(result.Get()) : NotFound();
    }

    private async Task<IActionResult?> AuthorizeOwnerAsync(
        Guid organizationId,
        CancellationToken cancellationToken
    )
    {
        var access = await _organizationAccess
            .CheckOwnerAccessAsync(
                OrganizationId.From(organizationId),
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

        return null;
    }
}
