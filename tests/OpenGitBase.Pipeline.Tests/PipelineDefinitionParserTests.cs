using OpenGitBase.Pipeline;

namespace OpenGitBase.Pipeline.Tests;

public class PipelineDefinitionParserTests
{
    private static readonly string FixtureRoot = Path.Combine(AppContext.BaseDirectory, "fixtures");

    [Fact]
    public void ParsePipelineDefinition_MergesDefaults_AndResolvesJobUsers()
    {
        var yaml = File.ReadAllText(Path.Combine(FixtureRoot, "valid-defaults.yml"));

        var result = PipelineDefinitionParser.ParsePipelineDefinition(yaml);

        Assert.True(result.IsValid);
        var definition = Assert.IsType<PipelineDefinition>(result.Definition);
        Assert.Equal(2, definition.Jobs.Count);
        var build = Assert.Single(definition.Jobs.Where(job => job.Name == "build"));
        Assert.Equal("alpine:latest", build.Image);
        Assert.Equal("ogb", build.ScriptUser);
        Assert.Equal("root", build.InstallScriptUser);
        Assert.Equal("0", build.Variables["GIT_DEPTH"]);
        Assert.Single(build.Dependencies);
    }

    [Fact]
    public void ParsePipelineDefinition_UsesFirstSeenStageOrder_WhenStagesMissing()
    {
        var yaml = """
            image: alpine:latest
            build:
              stage: build
              runs-on: ogb-hosted
              script: echo build
            test:
              stage: test
              runs-on: ogb-hosted
              script: echo test
            lint:
              stage: build
              runs-on: ogb-hosted
              script: echo lint
            """;

        var result = PipelineDefinitionParser.ParsePipelineDefinition(yaml);

        Assert.True(result.IsValid);
        var definition = Assert.IsType<PipelineDefinition>(result.Definition);
        Assert.Equal(new[] { "build", "test" }, definition.Stages);
    }

    [Fact]
    public void ParsePipelineDefinition_RejectsMissingRunsOn_AndUnsupportedOnlyPattern()
    {
        var yaml = File.ReadAllText(Path.Combine(FixtureRoot, "invalid-runs-on-and-only.yml"));

        var result = PipelineDefinitionParser.ParsePipelineDefinition(yaml);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Path == "$.build.runs-on");
        Assert.Contains(result.Errors, error => error.Path == "$.build.only");
    }

    [Theory]
    [InlineData("main", "main", true)]
    [InlineData("release/*", "release/1.0.0", true)]
    [InlineData("release/*", "release/1/0", true)]
    [InlineData("release/*", "main", false)]
    [InlineData("release/**", "release/1.0.0", false)]
    public void OnlyGlobMatcher_MatchesV1Patterns(string pattern, string input, bool expected)
    {
        var isMatch = OnlyGlobMatcher.IsMatch(pattern, input);
        Assert.Equal(expected, isMatch);
    }
}
