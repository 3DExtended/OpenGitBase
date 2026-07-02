using System.Text.Json;
using OpenGitBase.E2E.Core;
using OpenGitBase.E2E.Core.Fixtures;

namespace OpenGitBase.E2E.Tests.MergeRequest;

[Collection("Compose")]
[Trait("Category", "MergeRequest")]
[Trait("RequiresCompose", "true")]
[E2eTier(6)]
public class MergeRequestDiscussionLinksE2eTests : E2eTestBase
{
    [RequiresComposeFact]
    public async Task LinkListAndRemoveDiscussionFromMergeRequest()
    {
        BeginScenario();
        var identity = new IdentityFixture(Context, Transcript);
        var mergeFixture = new MergeRequestFixture(Transcript, Context.Normalizer);
        var owner = await identity.RegisterUserAsync($"mr-link-owner-{Context.RunSuffix}").ConfigureAwait(false);
        var writer = await identity.RegisterUserAsync($"mr-link-writer-{Context.RunSuffix}").ConfigureAwait(false);
        var repoSlug = $"mr-link-{Context.RunSuffix}";
        var workRoot = Path.Combine(Path.GetTempPath(), $"e2e-mr-link-{Context.RunSuffix}");

        try
        {
            var seed = await mergeFixture.SeedMrReadyAsync(
                owner,
                writer,
                repoSlug,
                workRoot).ConfigureAwait(false);

            Transcript.Describe("Create discussions to link from merge request");
            var discussionBase = $"/repository/by-slug/{owner.Username}/{repoSlug}/discussions";
            var createDiscussion = await owner.Client.PostAsync(discussionBase, new
            {
                title = "Refactor auth module",
                body = "Track MR work here.",
            }).ConfigureAwait(false);
            Assert.True(createDiscussion.StatusCode is 200 or 201, createDiscussion.Body);
            var discussionNumber = ParseInt(createDiscussion.Body, "number");

            var createRelatedDiscussion = await owner.Client.PostAsync(discussionBase, new
            {
                title = "Follow-up docs",
                body = "Informational only.",
            }).ConfigureAwait(false);
            Assert.True(createRelatedDiscussion.StatusCode is 200 or 201, createRelatedDiscussion.Body);
            var relatedDiscussionNumber = ParseInt(createRelatedDiscussion.Body, "number");

            Transcript.Describe("Create merge request");
            var createMr = await owner.Client.PostAsync(seed.MergeRequestBase, new
            {
                title = "Implement auth refactor",
                body = "Closes discussion tracking.",
                sourceRef = seed.FeatureBranch,
                targetRef = "main",
                isDraft = false,
            }).ConfigureAwait(false);
            Assert.True(createMr.StatusCode is 200 or 201, createMr.Body);
            var mergeRequestNumber = ParseInt(createMr.Body, "number");
            var linksBase = $"{seed.MergeRequestBase}/{mergeRequestNumber}/discussion-links";

            Transcript.Describe("Link discussion with closes relationship");
            var createLink = await owner.Client.PostAsync(linksBase, new
            {
                discussionNumber,
                relationshipType = "closes",
            }).ConfigureAwait(false);
            Assert.True(createLink.StatusCode is 200 or 201, createLink.Body);
            Assert.Contains("Refactor auth module", createLink.Body, StringComparison.Ordinal);
            await Baselines.CaptureApiAsync("create-discussion-link", createLink).ConfigureAwait(false);

            Transcript.Describe("Link second discussion as related");
            var createRelatedLink = await owner.Client.PostAsync(linksBase, new
            {
                discussionNumber = relatedDiscussionNumber,
                relationshipType = "related",
            }).ConfigureAwait(false);
            Assert.True(createRelatedLink.StatusCode is 200 or 201, createRelatedLink.Body);
            await Baselines.CaptureApiAsync("create-related-discussion-link", createRelatedLink).ConfigureAwait(false);

            Transcript.Describe("List linked discussions on merge request");
            var listLinks = await owner.Client.GetAsync(linksBase).ConfigureAwait(false);
            Assert.Equal(200, listLinks.StatusCode);
            using (var listDoc = JsonDocument.Parse(listLinks.Body))
            {
                var links = listDoc.RootElement.EnumerateArray().ToList();
                Assert.Equal(2, links.Count);
                Assert.Contains(links, link => link.GetProperty("discussionNumber").GetInt32() == discussionNumber);
                Assert.Contains(links, link => link.GetProperty("discussionNumber").GetInt32() == relatedDiscussionNumber);
                Assert.Contains(links, link => link.GetProperty("relationshipType").GetInt32() == 0);
                Assert.Contains(links, link => link.GetProperty("relationshipType").GetInt32() == 1);
            }

            await Baselines.CaptureApiAsync("list-discussion-links", listLinks).ConfigureAwait(false);

            Transcript.Describe("Invalid discussion number is rejected");
            var invalidLink = await owner.Client.PostAsync(linksBase, new
            {
                discussionNumber = 99999,
                relationshipType = "related",
            }).ConfigureAwait(false);
            Assert.Equal(404, invalidLink.StatusCode);
            await Baselines.CaptureApiAsync("create-discussion-link-missing", invalidLink).ConfigureAwait(false);

            Transcript.Describe("Remove linked discussion");
            var deleteLink = await owner.Client.SendAsync(
                HttpMethod.Delete,
                $"{linksBase}/{discussionNumber}?relationshipType=closes").ConfigureAwait(false);
            Assert.Equal(204, deleteLink.StatusCode);

            var listAfterDelete = await owner.Client.GetAsync(linksBase).ConfigureAwait(false);
            Assert.Equal(200, listAfterDelete.StatusCode);
            using (var listDoc = JsonDocument.Parse(listAfterDelete.Body))
            {
                var links = listDoc.RootElement.EnumerateArray().ToList();
                Assert.Single(links);
                Assert.Equal(relatedDiscussionNumber, links[0].GetProperty("discussionNumber").GetInt32());
            }

            await Baselines.CaptureApiAsync("list-discussion-links-after-delete", listAfterDelete).ConfigureAwait(false);
            await AssertBaselinesAsync().ConfigureAwait(false);
        }
        finally
        {
            if (Directory.Exists(workRoot))
            {
                Directory.Delete(workRoot, true);
            }
        }
    }

    private static int ParseInt(string body, string propertyName)
    {
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty(propertyName).GetInt32();
    }
}
