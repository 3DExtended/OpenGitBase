using System.Diagnostics;
using System.Text;

namespace OpenGitBase.E2E.Core;

public sealed class TestResultRecord
{
    public string TestName { get; init; } = string.Empty;

    public string ClassName { get; init; } = string.Empty;

    public int Tier { get; init; }

    public string Status { get; init; } = "Passed";

    public string Transcript { get; init; } = string.Empty;

    public IReadOnlyList<BaselineDiff> Diffs { get; init; } = Array.Empty<BaselineDiff>();

    public string? FailureMessage { get; init; }
}

public sealed class ReportGenerator
{
    public string Generate(
        IReadOnlyList<TierSummary> tiers,
        IReadOnlyList<TestResultRecord> tests,
        string? playwrightSectionHtml = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html><head><meta charset=\"utf-8\"><title>OpenGitBase E2E Report</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font-family:system-ui,sans-serif;margin:2rem;color:#111}");
        sb.AppendLine(".pass{color:#0a0}.fail{color:#c00}.skip{color:#888}");
        sb.AppendLine("pre{background:#f5f5f5;padding:1rem;overflow:auto}");
        sb.AppendLine(".diff del{background:#fdd}.diff ins{background:#dfd}");
        sb.AppendLine("table{border-collapse:collapse;width:100%}td,th{border:1px solid #ccc;padding:.5rem}");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine("<h1>OpenGitBase E2E Regression Report</h1>");
        sb.AppendLine($"<p>Generated {DateTimeOffset.UtcNow:u}</p>");

        sb.AppendLine("<h2>Tier Summary</h2><table><tr><th>Tier</th><th>Name</th><th>Status</th><th>Passed</th><th>Failed</th><th>Skipped</th><th>Skip Reason</th></tr>");
        foreach (var tier in tiers)
        {
            sb.AppendLine(
                $"<tr><td>{tier.Id}</td><td>{Escape(tier.Name)}</td><td class=\"{tier.Status.ToLowerInvariant()}\">{Escape(tier.Status)}</td>" +
                $"<td>{tier.Passed}</td><td>{tier.Failed}</td><td>{tier.Skipped}</td><td>{Escape(tier.SkipReason ?? string.Empty)}</td></tr>");
        }

        sb.AppendLine("</table>");

        sb.AppendLine("<h2>Tests</h2>");
        foreach (var test in tests)
        {
            sb.AppendLine($"<section><h3 class=\"{test.Status.ToLowerInvariant()}\">{Escape(test.ClassName)}.{Escape(test.TestName)} (Tier {test.Tier}) — {Escape(test.Status)}</h3>");
            if (!string.IsNullOrEmpty(test.FailureMessage))
            {
                sb.AppendLine($"<p><strong>Failure:</strong> {Escape(test.FailureMessage)}</p>");
            }

            sb.AppendLine("<h4>Transcript</h4><pre>" + Escape(test.Transcript) + "</pre>");
            if (test.Diffs.Count > 0)
            {
                sb.AppendLine("<h4>Baseline Diffs</h4>");
                foreach (var diff in test.Diffs)
                {
                    sb.AppendLine($"<div class=\"diff\"><strong>{Escape(diff.Path)}</strong><pre>Expected:\n{Escape(diff.Expected)}\n\nActual:\n{Escape(diff.Actual)}</pre></div>");
                }
            }

            sb.AppendLine("</section>");
        }

        if (!string.IsNullOrEmpty(playwrightSectionHtml))
        {
            sb.AppendLine("<h2>Playwright</h2>");
            sb.AppendLine(playwrightSectionHtml);
        }

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    public async Task<string> WriteReportAsync(
        IReadOnlyList<TierSummary> tiers,
        IReadOnlyList<TestResultRecord> tests,
        string? playwrightSectionHtml = null,
        CancellationToken cancellationToken = default)
    {
        var runId = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
        var reportDir = Path.Combine(E2eEnvironment.ReportsDirectory, runId);
        Directory.CreateDirectory(reportDir);
        var indexPath = Path.Combine(reportDir, "index.html");
        var html = Generate(tiers, tests, playwrightSectionHtml);
        await File.WriteAllTextAsync(indexPath, html, cancellationToken).ConfigureAwait(false);

        var latestPath = Path.Combine(E2eEnvironment.ReportsDirectory, "latest");
        if (Directory.Exists(latestPath))
        {
            Directory.Delete(latestPath, true);
        }

        Directory.CreateDirectory(latestPath);
        await File.WriteAllTextAsync(Path.Combine(latestPath, "index.html"), html, cancellationToken).ConfigureAwait(false);
        return indexPath;
    }

    private static string Escape(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}

public sealed class BrowserLauncher
{
    public void Open(string path)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true,
        };

        if (OperatingSystem.IsMacOS())
        {
            startInfo.FileName = "open";
            startInfo.ArgumentList.Add(path);
        }
        else if (OperatingSystem.IsLinux())
        {
            startInfo.FileName = "xdg-open";
            startInfo.ArgumentList.Add(path);
        }
        else if (OperatingSystem.IsWindows())
        {
            startInfo.FileName = path;
        }

        Process.Start(startInfo);
    }
}
