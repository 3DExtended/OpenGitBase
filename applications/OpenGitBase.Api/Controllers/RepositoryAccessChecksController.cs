using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;

namespace OpenGitBase.Api.Controllers;

[ApiController]
[AllowAnonymous]
[EnableRateLimiting("sensitive")]
[Route("api/v1/access-checks/repositories")]
public sealed class RepositoryAccessChecksController : ControllerBase
{
    private readonly RepositoryAccessCheckEvaluator _evaluator;

    public RepositoryAccessChecksController(
        IQueryProcessor queryProcessor,
        ISshKeyService sshKeyService,
        GitPushEnforcementService gitPushEnforcementService,
        ILogger<RepositoryAccessChecksController> logger,
        RepositoryStorageQuotaOptions quotaOptions,
        PlatformMergeIdentityOptions platformMergeOptions
    )
    {
        _evaluator = new RepositoryAccessCheckEvaluator(
            queryProcessor,
            sshKeyService,
            gitPushEnforcementService,
            logger,
            quotaOptions,
            platformMergeOptions
        );
    }

    [HttpPost]
    [ProducesResponseType(typeof(RepositoryAccessCheckResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RepositoryAccessCheckResponse>> CheckRepositoryAccess(
        [FromBody] RepositoryAccessCheckRequest request,
        CancellationToken cancellationToken
    )
    {
        var evaluation = await _evaluator.EvaluateAsync(request, cancellationToken);
        if (evaluation.ValidationErrors is { } validationErrors)
        {
            ModelState.Merge(validationErrors);
            return ValidationProblem(ModelState);
        }

        return Ok(evaluation.Response);
    }
}
