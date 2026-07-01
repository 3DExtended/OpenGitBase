using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.SendGrid;

namespace OpenGitBase.Api.Controllers.Internal;

[ApiController]
[Route("internal/e2e")]
public sealed class E2eController : ControllerBase
{
    private static readonly HashSet<string> TruncatedTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "Users",
        "UserCredentials",
        "Repository",
        "RepositoryMember",
        "RepositoryReplica",
        "GitAccessToken",
        "Organization",
        "OrganizationMember",
        "OrganizationInvite",
        "PublicGitSshKey",
        "ProtectedBranchRule",
        "ProtectedBranchAllowedUser",
        "PushRule",
        "discussions",
        "discussion_comments",
        "comment_anchors",
        "discussion_subscriptions",
        "discussion_tag_assignments",
        "repository_blocked_users",
        "repository_tags",
        "user_notifications",
        "merge_requests",
        "merge_request_approvals",
        "merge_request_discussion_links",
    };

    private readonly CapturingEmailStore _emailStore;
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public E2eController(
        CapturingEmailStore emailStore,
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IWebHostEnvironment environment,
        IConfiguration configuration)
    {
        _emailStore = emailStore;
        _contextFactory = contextFactory;
        _environment = environment;
        _configuration = configuration;
    }

    [HttpGet("emails")]
    [AllowAnonymous]
    public IActionResult GetEmails([FromQuery] string? to)
    {
        if (!IsE2eEnabled())
        {
            return NotFound();
        }

        return Ok(_emailStore.GetByRecipient(to));
    }

    [HttpPost("emails/clear")]
    [AllowAnonymous]
    public IActionResult ClearEmails()
    {
        if (!IsE2eEnabled())
        {
            return NotFound();
        }

        _emailStore.Clear();
        return NoContent();
    }

    [HttpPost("reset-database")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetDatabaseAsync(CancellationToken cancellationToken)
    {
        if (!IsE2eEnabled())
        {
            return NotFound();
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var tableNames = context.Model.GetEntityTypes()
            .Select(entityType => entityType.GetTableName())
            .Where(name => !string.IsNullOrWhiteSpace(name) && TruncatedTables.Contains(name!))
            .Distinct()
            .Select(name => $"\"{name}\"")
            .ToList();

        if (tableNames.Count == 0)
        {
            return NoContent();
        }

        var sql = $"TRUNCATE {string.Join(", ", tableNames)} RESTART IDENTITY CASCADE";
        await context.Database.ExecuteSqlRawAsync(sql, cancellationToken).ConfigureAwait(false);
        _emailStore.Clear();
        return NoContent();
    }

    [HttpPost("send-test-email")]
    [AllowAnonymous]
    public IActionResult SendTestEmail([FromBody] CapturedEmailMessage message)
    {
        if (!IsE2eEnabled())
        {
            return NotFound();
        }

        _emailStore.Add(message);
        return Ok();
    }

    private bool IsE2eEnabled()
    {
        if (!_environment.IsDevelopment() && !_environment.IsEnvironment("E2ETest"))
        {
            return false;
        }

        return string.Equals(_configuration["E2E:CaptureEmail"], "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(_configuration["E2E__CaptureEmail"], "true", StringComparison.OrdinalIgnoreCase);
    }
}
