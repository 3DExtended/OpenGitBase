using System.Diagnostics;
using OpenGitBase.E2E.Core;

var options = RunOptions.Parse(args);
Directory.SetCurrentDirectory(E2eEnvironment.RepoRoot);

var compose = new ComposeEnvironment();
var orchestrator = new TierOrchestrator();
var reportGenerator = new ReportGenerator();
var browser = new BrowserLauncher();
var tierSummaries = new List<TierSummary>();
var failedTier = -1;
var exitCode = 0;
PlaywrightRunResult? playwrightResult = null;

try
{
    var tierSkipsCompose = options.TierOnly == 8;
    if (!options.SkipCompose && !tierSkipsCompose)
    {
        Console.WriteLine($"Starting compose profile: {options.Profile}");
        await compose.StartAsync(options.Profile).ConfigureAwait(false);
    }
    else if (options.SkipCompose)
    {
        Console.WriteLine("Skipping compose (--skip-compose).");
    }
    else
    {
        Console.WriteLine("Skipping compose (Playwright-only --tier 8).");
    }

    if (options.UpdateBaselines)
    {
        Environment.SetEnvironmentVariable("OPENGITBASE_E2E_UPDATE_BASELINES", "1");
    }

    foreach (var tier in orchestrator.Tiers)
    {
        if (options.TierOnly is int onlyTier && tier.Id != onlyTier)
        {
            tierSummaries.Add(new TierSummary
            {
                Id = tier.Id,
                Name = tier.Name,
                Status = "Skipped",
                SkipReason = $"Not selected (--tier {onlyTier})",
            });
            continue;
        }

        if (failedTier >= 0)
        {
            tierSummaries.Add(new TierSummary
            {
                Id = tier.Id,
                Name = tier.Name,
                Status = "Skipped",
                SkipReason = $"Tier {failedTier} failed",
            });
            continue;
        }

        if (tier.Id == 7 && options.Profile != ComposeProfile.FullHa)
        {
            tierSummaries.Add(new TierSummary
            {
                Id = tier.Id,
                Name = tier.Name,
                Status = "Skipped",
                SkipReason = "Requires --profile full-ha",
            });
            continue;
        }

        if (tier.Id == 9 && !options.Fuzz)
        {
            tierSummaries.Add(new TierSummary { Id = tier.Id, Name = tier.Name, Status = "Skipped", SkipReason = "--fuzz not set" });
            continue;
        }

        if (tier.Id == 8)
        {
            var pw = new PlaywrightInvoker();
            playwrightResult = await pw.RunRegressionAsync().ConfigureAwait(false);
            tierSummaries.Add(new TierSummary
            {
                Id = tier.Id,
                Name = tier.Name,
                Status = playwrightResult.ExitCode == 0 ? "Passed" : "Failed",
                Passed = playwrightResult.ExitCode == 0 ? 1 : 0,
                Failed = playwrightResult.ExitCode == 0 ? 0 : 1,
            });
            if (playwrightResult.ExitCode != 0)
            {
                failedTier = tier.Id;
                exitCode = playwrightResult.ExitCode;
                if (tier.FailFast)
                {
                    break;
                }
            }

            continue;
        }

        var filter = options.BuildTestFilter(orchestrator.BuildDotnetTestFilter(tier.Id, null));
        var testExit = await RunDotnetTestAsync(filter, options).ConfigureAwait(false);
        tierSummaries.Add(new TierSummary
        {
            Id = tier.Id,
            Name = tier.Name,
            Status = testExit == 0 ? "Passed" : "Failed",
            Passed = testExit == 0 ? 1 : 0,
            Failed = testExit == 0 ? 0 : 1,
        });

        if (testExit != 0)
        {
            exitCode = testExit;
            failedTier = tier.Id;
            if (tier.FailFast)
            {
                tierSummaries.AddRange(orchestrator.BuildSkipSummaries(tier.Id, $"Tier {tier.Id} failed"));
                break;
            }
        }
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    exitCode = 1;
    tierSummaries.Add(new TierSummary { Id = -1, Name = "Runner", Status = "Failed", Failed = 1, SkipReason = ex.Message });
}
finally
{
    if (!options.SkipCompose && compose.StartedByRunner)
    {
        try
        {
            await compose.StopAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Compose stop failed: {ex.Message}");
        }
    }
}

var reportPath = await reportGenerator.WriteReportAsync(tierSummaries, [], playwrightResult).ConfigureAwait(false);
Console.WriteLine($"Report: {reportPath}");

var shouldOpen = options.OpenReport switch
{
    OpenReportMode.OpenAlways => true,
    OpenReportMode.OpenNever => false,
    _ => exitCode != 0,
};

if (shouldOpen)
{
    browser.Open(reportPath);
}

return exitCode;

static async Task<int> RunDotnetTestAsync(string filter, RunOptions options)
{
    Environment.SetEnvironmentVariable(ComposeFullHaGate.ProfileEnvironmentVariable, options.Profile.ToString());
    var testsProject = Path.Combine(E2eEnvironment.RepoRoot, "tests", "OpenGitBase.E2E.Tests", "OpenGitBase.E2E.Tests.csproj");
    var startInfo = new ProcessStartInfo
    {
        FileName = "dotnet",
        WorkingDirectory = E2eEnvironment.RepoRoot,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };
    startInfo.ArgumentList.Add("test");
    startInfo.ArgumentList.Add(testsProject);
    startInfo.ArgumentList.Add("--filter");
    startInfo.ArgumentList.Add(filter);

    using var process = Process.Start(startInfo)
        ?? throw new InvalidOperationException("Failed to start dotnet test.");
    await process.WaitForExitAsync().ConfigureAwait(false);
    var stdout = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
    Console.WriteLine(stdout);
    var stderr = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
    if (!string.IsNullOrEmpty(stderr))
    {
        Console.Error.WriteLine(stderr);
    }

    return process.ExitCode;
}
