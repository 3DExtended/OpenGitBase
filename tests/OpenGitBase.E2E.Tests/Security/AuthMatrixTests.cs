using OpenGitBase.E2E.Core;

namespace OpenGitBase.E2E.Tests.Security;

[Collection("Compose")]
[Trait("Category", "Security")]
[Trait("RequiresCompose", "true")]
[E2eTier(3)]
public class AuthMatrixTests : E2eTestBase
{
    public static IEnumerable<object[]> AnonymousMutationCases() =>
    [
        ["/repository/protected-slug", HttpMethod.Post, 401],
    ];

    [RequiresComposeTheory]
    [MemberData(nameof(AnonymousMutationCases))]
    public async Task AnonymousCannotMutate(string path, HttpMethod method, int expectedStatus)
    {
        BeginScenario($"{method}-{path.Trim('/')}");
        var anon = new E2eApiClient(Transcript, Context.Normalizer);
        Transcript.Describe($"Anonymous {method} {path} should return {expectedStatus}");
        var result = await anon.SendAsync(method, path, new { repositoryName = "x", isPrivate = true }).ConfigureAwait(false);
        await Baselines.CaptureApiAsync($"anon-{method}-{path.Trim('/')}", result).ConfigureAwait(false);
        Assert.Equal(expectedStatus, result.StatusCode);
        await AssertBaselinesAsync().ConfigureAwait(false);
    }
}
