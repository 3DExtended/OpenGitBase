using System.Text.Json;
using OpenGitBase.E2E.Core;
using OpenGitBase.E2E.Core.Fixtures;

namespace OpenGitBase.E2E.Tests.Discussion;

[Collection("Compose")]
[Trait("Category", "Discussion")]
[Trait("Tag", "Smoke")]
[Trait("RequiresCompose", "true")]
[E2eTier(4)]
public class DiscussionSmokeTests : E2eTestBase
{
    private readonly GitTestDataFixture _gitData;

    public DiscussionSmokeTests()
    {
        _gitData = new GitTestDataFixture(Transcript, Context.Normalizer);
    }

    [RequiresComposeFact]
    public async Task CommentOnResolvedDiscussionReopensIt()
    {
        BeginScenario();
        var setup = await SeedDiscussionScenarioAsync("reopen").ConfigureAwait(false);
        var baseUrl = $"/repository/by-slug/{setup.Owner.Username}/{setup.RepoSlug}/discussions";

        Transcript.Describe("Resolve discussion then reopen by posting comment");
        var create = await setup.Owner.Client.PostAsync(baseUrl, new { title = "needs revisit", body = "first" }).ConfigureAwait(false);
        Assert.True(create.StatusCode is 200 or 201, create.Body);
        var number = ParseDiscussionNumber(create);

        var resolve = await setup.Owner.Client.PostAsync($"{baseUrl}/{number}/resolve", null).ConfigureAwait(false);
        Assert.Equal(200, resolve.StatusCode);

        var reopen = await setup.Reader.Client.PostAsync($"{baseUrl}/{number}/comments", new { bodyMarkdown = "reopening comment" })
            .ConfigureAwait(false);
        Assert.Equal(200, reopen.StatusCode);

        var detail = await setup.Owner.Client.GetAsync($"{baseUrl}/{number}").ConfigureAwait(false);
        Assert.Equal(200, detail.StatusCode);
        Assert.Contains("\"status\":0", detail.Body, StringComparison.Ordinal);
        await Baselines.CaptureApiAsync("reopen-via-comment", detail).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task BlockAndUnblockUserControlsCommenting()
    {
        BeginScenario();
        var setup = await SeedDiscussionScenarioAsync("block").ConfigureAwait(false);
        var baseUrl = $"/repository/by-slug/{setup.Owner.Username}/{setup.RepoSlug}";
        var discussionBase = $"{baseUrl}/discussions";

        Transcript.Describe("Block user prevents discussion comments, unblock restores access");
        var create = await setup.Owner.Client.PostAsync(discussionBase, new { title = "moderation", body = "seed" }).ConfigureAwait(false);
        Assert.True(create.StatusCode is 200 or 201, create.Body);
        var number = ParseDiscussionNumber(create);

        var block = await setup.Owner.Client.PostAsync($"{baseUrl}/blocked-users", new
        {
            userId = setup.Reader.UserId,
            reason = "smoke block",
        }).ConfigureAwait(false);
        Assert.Equal(200, block.StatusCode);

        var blockedComment = await setup.Reader.Client.PostAsync($"{discussionBase}/{number}/comments", new { bodyMarkdown = "blocked" })
            .ConfigureAwait(false);
        Assert.Equal(403, blockedComment.StatusCode);

        var unblock = await setup.Owner.Client.SendAsync(HttpMethod.Delete, $"{baseUrl}/blocked-users/{setup.Reader.UserId}")
            .ConfigureAwait(false);
        Assert.Equal(204, unblock.StatusCode);

        var commentAfterUnblock = await setup.Reader.Client.PostAsync($"{discussionBase}/{number}/comments", new
        {
            bodyMarkdown = "allowed again",
        }).ConfigureAwait(false);
        Assert.Equal(200, commentAfterUnblock.StatusCode);
        await Baselines.CaptureApiAsync("block-unblock-comment", commentAfterUnblock).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task NewCommentCreatesNotification()
    {
        BeginScenario();
        var setup = await SeedDiscussionScenarioAsync("notify").ConfigureAwait(false);
        var baseUrl = $"/repository/by-slug/{setup.Owner.Username}/{setup.RepoSlug}/discussions";

        Transcript.Describe("Commenting creates unread notification for discussion owner");
        var create = await setup.Owner.Client.PostAsync(baseUrl, new { title = "notify me", body = "seed" }).ConfigureAwait(false);
        Assert.True(create.StatusCode is 200 or 201, create.Body);
        var number = ParseDiscussionNumber(create);

        var comment = await setup.Reader.Client.PostAsync($"{baseUrl}/{number}/comments", new { bodyMarkdown = "new comment" })
            .ConfigureAwait(false);
        Assert.Equal(200, comment.StatusCode);

        var notifications = await setup.Owner.Client.GetAsync("/notifications?unreadOnly=true").ConfigureAwait(false);
        Assert.Equal(200, notifications.StatusCode);
        Assert.Contains("New comment", notifications.Body, StringComparison.Ordinal);
        await Baselines.CaptureApiAsync("notification-list", notifications).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task NotificationEmailSubjectContainsDiscussionPrefix()
    {
        BeginScenario();
        var setup = await SeedDiscussionScenarioAsync("email").ConfigureAwait(false);
        var baseUrl = $"/repository/by-slug/{setup.Owner.Username}/{setup.RepoSlug}/discussions";
        await EmailCapture.ClearAsync().ConfigureAwait(false);

        Transcript.Describe("Comment notification emits email with expected subject prefix");
        var create = await setup.Owner.Client.PostAsync(baseUrl, new { title = "email subject", body = "seed" }).ConfigureAwait(false);
        Assert.True(create.StatusCode is 200 or 201, create.Body);
        var number = ParseDiscussionNumber(create);

        var comment = await setup.Reader.Client.PostAsync($"{baseUrl}/{number}/comments", new { bodyMarkdown = "mail trigger" })
            .ConfigureAwait(false);
        Assert.Equal(200, comment.StatusCode);

        var emails = await EmailCapture.ListAsync().ConfigureAwait(false);
        var ownerEmail = $"{setup.Owner.Username}@example.com";
        var notification = emails.LastOrDefault(m =>
            m.To.Contains(ownerEmail, StringComparison.OrdinalIgnoreCase)
            && m.Subject.Contains($"[{setup.Owner.Username}/{setup.RepoSlug} #{number}]", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(notification);
        Assert.Contains("New comment", notification!.Subject, StringComparison.OrdinalIgnoreCase);
        await Baselines.CaptureSideChannelAsync("notification-email", "emails", new
        {
            notification.To,
            notification.Subject,
        }).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task TagFilterReturnsTaggedDiscussion()
    {
        BeginScenario();
        var setup = await SeedDiscussionScenarioAsync("tags").ConfigureAwait(false);
        var tagsUrl = $"/repository/by-slug/{setup.Owner.Username}/{setup.RepoSlug}/tags";
        var discussionsUrl = $"/repository/by-slug/{setup.Owner.Username}/{setup.RepoSlug}/discussions";

        Transcript.Describe("Tag filter includes only tagged discussion");
        var createTag = await setup.Owner.Client.PostAsync(tagsUrl, new { name = "smoke-tag", color = "#ff00ff" }).ConfigureAwait(false);
        Assert.Equal(200, createTag.StatusCode);
        var tagId = ParseNestedGuid(createTag.Body, "id");

        var createDiscussion = await setup.Owner.Client.PostAsync(discussionsUrl, new
        {
            title = "tagged discussion",
            body = "with tag",
            tagIds = new[] { tagId },
        }).ConfigureAwait(false);
        Assert.True(createDiscussion.StatusCode is 200 or 201, createDiscussion.Body);

        var filtered = await setup.Owner.Client.GetAsync($"{discussionsUrl}?tagId={tagId}").ConfigureAwait(false);
        Assert.Equal(200, filtered.StatusCode);
        Assert.Contains("tagged discussion", filtered.Body, StringComparison.Ordinal);
        await Baselines.CaptureApiAsync("tag-filter", filtered).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task AnchoredCommentIncludesAnchorPayload()
    {
        BeginScenario();
        var setup = await SeedDiscussionScenarioAsync("anchor").ConfigureAwait(false);
        var discussionsUrl = $"/repository/by-slug/{setup.Owner.Username}/{setup.RepoSlug}/discussions";
        var gitState = new GitAssertions().Inspect(setup.WorkDir);
        var commitSha = gitState.RecentCommits[0].Split(' ', 2)[0];

        Transcript.Describe("Anchored comment persists file anchor details");
        var createDiscussion = await setup.Owner.Client.PostAsync(discussionsUrl, new
        {
            title = "anchor check",
            body = "seed",
        }).ConfigureAwait(false);
        Assert.True(createDiscussion.StatusCode is 200 or 201, createDiscussion.Body);
        var number = ParseDiscussionNumber(createDiscussion);

        var anchored = await setup.Reader.Client.PostAsync($"{discussionsUrl}/{number}/comments", new
        {
            bodyMarkdown = "anchored feedback",
            anchor = new
            {
                @ref = "main",
                commitSha,
                filePath = GitTestDataLayout.AnchorPath,
                line = 2,
                endLine = 2,
            },
        }).ConfigureAwait(false);
        Assert.Equal(200, anchored.StatusCode);

        var detail = await setup.Owner.Client.GetAsync($"{discussionsUrl}/{number}?include=comments").ConfigureAwait(false);
        Assert.Equal(200, detail.StatusCode);
        Assert.Contains(GitTestDataLayout.AnchorPath, detail.Body, StringComparison.Ordinal);
        await Baselines.CaptureApiAsync("anchored-comment", detail).ConfigureAwait(false);
    }

    private async Task<DiscussionScenario> SeedDiscussionScenarioAsync(string prefix)
    {
        var identity = new IdentityFixture(Context, Transcript);
        var owner = await identity.RegisterUserAsync($"disc-smoke-owner-{prefix}-{Context.RunSuffix}").ConfigureAwait(false);
        var reader = await identity.RegisterUserAsync($"disc-smoke-reader-{prefix}-{Context.RunSuffix}").ConfigureAwait(false);
        var workDir = Path.Combine(Path.GetTempPath(), $"e2e-disc-smoke-{prefix}-{Context.RunSuffix}");
        var seeded = await _gitData.GetAnchorRepoAsync(owner, $"{prefix}-{Context.RunSuffix}", workDir).ConfigureAwait(false);
        return new DiscussionScenario(owner, reader, seeded.Slug, seeded.WorkDir);
    }

    private static int ParseDiscussionNumber(HttpCapture capture)
    {
        using var doc = JsonDocument.Parse(capture.Body);
        return doc.RootElement.GetProperty("number").GetInt32();
    }

    private static Guid ParseNestedGuid(string body, string propertyName)
    {
        using var doc = JsonDocument.Parse(body);
        var value = doc.RootElement.GetProperty(propertyName);
        return value.ValueKind == JsonValueKind.Object
            ? value.GetProperty("value").GetGuid()
            : value.GetGuid();
    }

    private sealed record DiscussionScenario(
        AuthenticatedClient Owner,
        AuthenticatedClient Reader,
        string RepoSlug,
        string WorkDir);
}
