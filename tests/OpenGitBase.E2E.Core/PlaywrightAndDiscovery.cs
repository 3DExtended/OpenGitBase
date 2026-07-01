using System.Diagnostics;
using HtmlAgilityPack;

namespace OpenGitBase.E2E.Core;

public interface ICoverageRegistry
{
    bool IsCovered(string urlPattern);

    void RegisterDiscovered(string url, string sourceTest);

    IReadOnlyList<DiscoveredUrl> Discovered { get; }
}

public sealed class DiscoveredUrl
{
    public string Url { get; init; } = string.Empty;

    public string SourceTest { get; init; } = string.Empty;

    public DateTimeOffset DiscoveredAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class UrlDiscovery : ICoverageRegistry
{
    private readonly HashSet<string> _covered = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<DiscoveredUrl> _discovered = [];

    public IReadOnlyList<DiscoveredUrl> Discovered => _discovered;

    public void RegisterCovered(string urlPattern) => _covered.Add(Normalize(urlPattern));

    public bool IsCovered(string urlPattern) => _covered.Contains(Normalize(urlPattern));

    public void RegisterDiscovered(string url, string sourceTest)
    {
        var normalized = Normalize(url);
        if (_covered.Contains(normalized))
        {
            return;
        }

        _discovered.Add(new DiscoveredUrl { Url = normalized, SourceTest = sourceTest });
    }

    public IReadOnlyList<string> ExtractLinks(string html, string sourceTest)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var links = doc.DocumentNode.SelectNodes("//a[@href]")
            ?.Select(n => n.GetAttributeValue("href", string.Empty))
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? [];

        foreach (var link in links)
        {
            if (!IsCovered(link))
            {
                RegisterDiscovered(link, sourceTest);
            }
        }

        return links;
    }

    private static string Normalize(string url) => url.Trim().TrimEnd('/');
}

public interface ITestGenerator
{
    Task GenerateSkeletonAsync(DiscoveredUrl url, CancellationToken cancellationToken = default);
}

public sealed class TestGenerator : ITestGenerator
{
    public async Task GenerateSkeletonAsync(DiscoveredUrl url, CancellationToken cancellationToken = default)
    {
        var className = "Discovered_" + Sanitize(url.Url);
        var targetDir = Path.Combine(E2eEnvironment.RepoRoot, "tests", "OpenGitBase.E2E.Tests", "Discovered");
        Directory.CreateDirectory(targetDir);
        var filePath = Path.Combine(targetDir, $"{className}.cs");
        if (File.Exists(filePath))
        {
            return;
        }

        var content = $$"""
            // Auto-generated {{DateTimeOffset.UtcNow:u}} from {{url.SourceTest}}
            // URL: {{url.Url}}
            using OpenGitBase.E2E.Core;

            namespace OpenGitBase.E2E.Tests.Discovered;

            [Trait("Category", "Discovered")]
            [E2eTier(2)]
            public class {{className}}
            {
                [Fact]
                public Task VisitDiscoveredUrl()
                {
                    throw new InvalidOperationException("Discovered URL requires --update-baselines before passing.");
                }
            }
            """;

        await File.WriteAllTextAsync(filePath, content, cancellationToken).ConfigureAwait(false);
    }

    private static string Sanitize(string url)
    {
        var chars = url.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray();
        return new string(chars).Trim('_');
    }
}

public sealed class PlaywrightInvoker
{
    public async Task<PlaywrightRunResult> RunRegressionAsync(CancellationToken cancellationToken = default)
    {
        var webRoot = Path.Combine(E2eEnvironment.RepoRoot, "applications", "opengitbase-web");
        var startInfo = new ProcessStartInfo
        {
            FileName = "npx",
            WorkingDirectory = webRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("playwright");
        startInfo.ArgumentList.Add("test");
        startInfo.ArgumentList.Add("--grep");
        startInfo.ArgumentList.Add("@regression");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start playwright.");
        var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        var reportDir = Path.Combine(webRoot, "playwright-report");
        var testResultsDir = Path.Combine(webRoot, "test-results");
        return new PlaywrightRunResult
        {
            ExitCode = process.ExitCode,
            StdOut = stdout,
            StdErr = stderr,
            ReportDirectory = Directory.Exists(reportDir) ? reportDir : null,
            TestResultsDirectory = Directory.Exists(testResultsDir) ? testResultsDir : null,
        };
    }
}

public sealed class PlaywrightRunResult
{
    public int ExitCode { get; init; }

    public string StdOut { get; init; } = string.Empty;

    public string StdErr { get; init; } = string.Empty;

    public string? ReportDirectory { get; init; }

    public string? TestResultsDirectory { get; init; }

    public async Task<string> ArchiveIntoReportAsync(string e2eReportDir, CancellationToken cancellationToken = default)
    {
        var archiveRoot = Path.Combine(e2eReportDir, "playwright");
        Directory.CreateDirectory(archiveRoot);

        if (ReportDirectory != null)
        {
            CopyDirectory(ReportDirectory, Path.Combine(archiveRoot, "html-report"));
        }

        var images = new List<string>();
        if (TestResultsDirectory != null)
        {
            foreach (var image in Directory.EnumerateFiles(TestResultsDirectory, "*.png", SearchOption.AllDirectories))
            {
                var name = image.Replace(TestResultsDirectory, string.Empty).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var dest = Path.Combine(archiveRoot, "artifacts", name);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(image, dest, overwrite: true);
                images.Add($"playwright/artifacts/{name.Replace('\\', '/')}");
            }
        }

        await File.WriteAllTextAsync(
            Path.Combine(archiveRoot, "stdout.txt"),
            string.IsNullOrWhiteSpace(StdErr) ? StdOut : $"{StdOut}\n\n--- stderr ---\n{StdErr}",
            cancellationToken).ConfigureAwait(false);

        return BuildEmbedHtml(images);
    }

    public string BuildEmbedHtml(IReadOnlyList<string> archivedImagePaths)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("<p>Playwright <code>@regression</code> specs (MSW visual gallery).</p>");
        if (ReportDirectory != null)
        {
            sb.Append("<p><a href=\"playwright/html-report/index.html\">Open Playwright HTML report</a></p>");
        }

        if (archivedImagePaths.Count > 0)
        {
            sb.Append("<h3>Screenshots &amp; diffs</h3><div class=\"pw-gallery\">");
            foreach (var relative in archivedImagePaths)
            {
                sb.Append($"<figure><img src=\"{relative}\" alt=\"{relative}\" loading=\"lazy\"/><figcaption>{relative}</figcaption></figure>");
            }

            sb.Append("</div>");
        }

        if (!string.IsNullOrWhiteSpace(StdOut))
        {
            sb.Append("<h3>Console</h3><pre>");
            sb.Append(System.Net.WebUtility.HtmlEncode(StdOut));
            sb.Append("</pre>");
        }

        return sb.ToString();
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var dest = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, overwrite: true);
        }
    }
}

public enum ExpectedOutcome
{
    Status401,
    Status403,
    Status404,
    Status400,
}

public sealed class ValidRequestTemplate
{
    public string Method { get; init; } = "GET";

    public string Url { get; init; } = string.Empty;

    public string? Body { get; init; }

    public string? BearerToken { get; init; }
}

public interface IFuzzScenario
{
    string Name { get; }

    ValidRequestTemplate Template { get; }

    ExpectedOutcome Expected { get; }
}

public sealed class FuzzResult
{
    public string ScenarioName { get; init; } = string.Empty;

    public int StatusCode { get; init; }

    public ExpectedOutcome Expected { get; init; }

    public bool Passed { get; init; }

    public string? Error { get; init; }
}

public interface IFuzzRunner
{
    Task<IReadOnlyList<FuzzResult>> RunAsync(IEnumerable<IFuzzScenario> scenarios, CancellationToken cancellationToken = default);
}

public sealed class FuzzRunner : IFuzzRunner
{
    public async Task<IReadOnlyList<FuzzResult>> RunAsync(IEnumerable<IFuzzScenario> scenarios, CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient { BaseAddress = E2eEnvironment.ApiBaseUrl };
        var results = new List<FuzzResult>();
        foreach (var scenario in scenarios)
        {
            var template = scenario.Template;
            var relativeUrl = template.Url.StartsWith('/') ? template.Url.TrimStart('/') : template.Url;
            using var request = new HttpRequestMessage(new HttpMethod(template.Method), relativeUrl);
            if (!string.IsNullOrEmpty(template.BearerToken))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", template.BearerToken);
            }

            if (!string.IsNullOrEmpty(template.Body))
            {
                request.Content = new StringContent(template.Body, System.Text.Encoding.UTF8, "application/json");
            }

            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var status = (int)response.StatusCode;
            var expectedStatus = MapExpectedStatus(scenario.Expected);
            var passed = status == expectedStatus;
            if (status >= 500)
            {
                passed = false;
            }

            results.Add(new FuzzResult
            {
                ScenarioName = scenario.Name,
                StatusCode = status,
                Expected = scenario.Expected,
                Passed = passed,
                Error = passed ? null : $"Expected {expectedStatus} but got {status}",
            });
        }

        return results;
    }

    private static int MapExpectedStatus(ExpectedOutcome outcome) =>
        outcome switch
        {
            ExpectedOutcome.Status401 => 401,
            ExpectedOutcome.Status403 => 403,
            ExpectedOutcome.Status404 => 404,
            ExpectedOutcome.Status400 => 400,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
        };
}
