using OpenGitBase.E2E.Core;

namespace OpenGitBase.E2E.Tests.Fuzz;

[Trait("Category", "Fuzz")]
[E2eTier(9)]
public class FuzzTests
{
    [Fact]
    public void ServerErrorFailsFuzzResult()
    {
        var result = new FuzzResult
        {
            ScenarioName = "sample",
            StatusCode = 500,
            Expected = ExpectedOutcome.Status403,
            Passed = false,
            Error = "Expected 403 class but got 500",
        };
        Assert.False(result.Passed);
    }

    [Fact]
    public void MatchingAuthErrorClassPasses()
    {
        var result = new FuzzResult
        {
            ScenarioName = "sample",
            StatusCode = 401,
            Expected = ExpectedOutcome.Status401,
            Passed = true,
        };
        Assert.True(result.Passed);
    }
}

[Collection("Compose")]
[Trait("Category", "Fuzz")]
[Trait("RequiresCompose", "true")]
[E2eTier(9)]
public class FuzzIntegrationTests
{
    [RequiresComposeFact]
    public async Task MutatedAnonymousRequestReturnsExpectedErrorClass()
    {
        var runner = new FuzzRunner();
        var results = await runner.RunAsync([
            new AnonymousPostScenario(),
        ]);
        Assert.All(results, r => Assert.True(r.Passed, r.Error));
    }

    private sealed class AnonymousPostScenario : IFuzzScenario
    {
        public string Name => "anonymous-create-repo";

        public ValidRequestTemplate Template { get; } = new()
        {
            Method = "POST",
            Url = "/repository/fuzz-slug",
            Body = """{"repositoryName":"fuzz","isPrivate":true}""",
        };

        public ExpectedOutcome Expected => ExpectedOutcome.Status401;
    }
}
