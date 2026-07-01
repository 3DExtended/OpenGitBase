using OpenGitBase.E2E.Core;

namespace OpenGitBase.E2E.Tests.Unit;

[Trait("Category", "E2EUnit")]
public class TranscriptTests
{
    [Fact]
    public void DescribeAndWireEventsPreserveOrder()
    {
        var transcript = new OperationTranscript();
        var normalizer = new BaselineNormalizer("suffix123");
        transcript.Describe("First intent");
        transcript.RecordWire(new WireEvent { Kind = WireEventKind.HttpRequest, Summary = "GET /health" });
        transcript.RecordWire(new WireEvent { Kind = WireEventKind.HttpResponse, Summary = "200 OK", StatusCode = 200 });
        var serialized = transcript.SerializeNormalized(normalizer);
        Assert.Contains("[Intent] First intent", serialized);
        Assert.Contains("[HttpRequest] GET /health", serialized);
        Assert.Contains("[HttpResponse] 200 OK", serialized);
    }
}

[Trait("Category", "E2EUnit")]
public class BaselineNormalizerTests
{
    [Fact]
    public void ReplacesRunSuffixAndGuids()
    {
        var normalizer = new BaselineNormalizer("abc123");
        var input = "user-abc123 id=550e8400-e29b-41d4-a716-446655440000";
        var result = normalizer.Normalize(input);
        Assert.Contains("{{RUN_SUFFIX}}", result);
        Assert.Contains("{{GUID}}", result);
        Assert.DoesNotContain("abc123", result);
    }

    [Fact]
    public void ReplacesHealthCheckDurations()
    {
        var normalizer = new BaselineNormalizer("suffix");
        var input = """{"totalDurationMs":12,"durationMs":3}""";
        var result = normalizer.Normalize(input);
        Assert.DoesNotContain(":12", result);
        Assert.Contains("{{DURATION_MS}}", result);
    }
}

[Trait("Category", "E2EUnit")]
public class ReportGeneratorTests
{
    [Fact]
    public void GenerateIncludesTierAndTestSections()
    {
        var generator = new ReportGenerator();
        var html = generator.Generate(
        [
            new TierSummary { Id = 0, Name = "Infrastructure", Status = "Passed", Passed = 1 },
        ],
        [
            new TestResultRecord
            {
                TestName = "Smoke",
                ClassName = "InfrastructureSmokeTests",
                Tier = 0,
                Status = "Passed",
                Transcript = "[Intent] health check",
            },
        ]);
        Assert.Contains("<h1>OpenGitBase E2E Regression Report</h1>", html);
        Assert.Contains("InfrastructureSmokeTests.Smoke", html);
        Assert.Contains("[Intent] health check", html);
    }
}

[Trait("Category", "E2EUnit")]
public class UrlDiscoveryTests
{
    [Fact]
    public async Task GeneratesSkeletonForUncoveredLink()
    {
        var discovery = new UrlDiscovery();
        discovery.RegisterCovered("/known");
        var links = discovery.ExtractLinks("""<a href="/new-page">x</a>""", "SmokeTest");
        Assert.Contains("/new-page", links);
        Assert.Single(discovery.Discovered);

        var generator = new TestGenerator();
        var targetDir = Path.Combine(E2eEnvironment.RepoRoot, "tests", "OpenGitBase.E2E.Tests", "Discovered");
        try
        {
            await generator.GenerateSkeletonAsync(discovery.Discovered[0]);
            Assert.True(Directory.GetFiles(targetDir, "Discovered_*.cs").Length > 0);
        }
        finally
        {
            var generated = Directory.Exists(targetDir)
                ? Directory.GetFiles(targetDir, "Discovered_*.cs")
                : [];
            foreach (var file in generated)
            {
                File.Delete(file);
            }
        }
    }
}
