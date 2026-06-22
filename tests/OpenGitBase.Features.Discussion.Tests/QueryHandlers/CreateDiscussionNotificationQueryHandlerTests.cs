namespace OpenGitBase.Features.Discussion.Tests.QueryHandlers;

public class CreateDiscussionNotificationQueryHandlerTests
{
    [Theory]
    [InlineData("acme", "demo", 42, "New comment", "[acme/demo #42] New comment")]
    [InlineData("owner", "repo", 1, "You were mentioned", "[owner/repo #1] You were mentioned")]
    public void EmailSubject_UsesStableOwnerRepoNumberPrefix(
        string ownerSlug,
        string repoSlug,
        int number,
        string message,
        string expectedSubject
    )
    {
        var subjectPrefix = $"[{ownerSlug}/{repoSlug} #{number}]";
        var subject = $"{subjectPrefix} {message}";
        Assert.Equal(expectedSubject, subject);
    }
}
