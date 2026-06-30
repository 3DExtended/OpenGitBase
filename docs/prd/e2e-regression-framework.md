# PRD: Unified E2E Regression Framework

## Problem Statement

OpenGitBase is a distributed forge: multiple API replicas behind HAProxy, Nuxt web frontends, dispatcher git edges, three storage nodes with replication, PostgreSQL, Redis, and SendGrid-backed email. Domain logic spans many feature modules (repositories, merge requests, discussions, organizations, git access tokens, storage fleet administration, and more), each with its own HTTP surface and user-visible workflows.

Today, regression coverage is fragmented:

- **Unit and handler tests** exercise CQRS handlers and controllers in isolation but do not prove end-to-end behavior across git, storage, and UI.
- **Shell integration scripts** (`e2e-https-git-test.sh`, merge-request e2e, HA storage e2e, discussion e2e, and similar) cover important paths but are easy to miss in review, hard to discover, inconsistent in output, and not unified with other test types.
- **Playwright visual specs** in the web application catch UI regressions but run separately, with no shared transcript or behavioral baseline model.
- **No single artifact** tells a manual regression reviewer what changed in the system — intended or not — when a change lands.

Reviewers and agents need a **deep, user-understandable test suite** that navigates the web UI, exercises git over HTTPS through the real dispatcher/storage path, sends adversarial API requests for authorization regressions, discovers untested URLs, and produces a **self-contained static report** combining human-readable operation steps with captured HTML, API responses, git state, and email side effects. Baselines must live in git so behavior changes are reviewable in diffs; reports stay local and ephemeral.

## Solution

Introduce a **C#-first E2E regression framework** orchestrated by a thin **runner executable** (the documented entry point for local regression). The runner:

1. Starts Docker Compose with a **selectable profile** (fast default vs full HA).
2. Executes tests in **ordered tiers** with dependency semantics (infrastructure and auth failures abort later tiers).
3. Auto-logs wire-level operations and accepts human intent annotations for readable transcripts.
4. Captures **committed behavioral baselines** (API, normalized HTML, git state, side-channel records including full email bodies) and diffs them on each run.
5. Invokes existing **Playwright TypeScript** specs (tagged subset) and embeds their artifacts in the unified report.
6. **Discovers untested URLs** during crawls and auto-generates skeleton tests under a `Discovered` area.
7. Writes a **dependency-free static HTML report** locally (not committed) and opens it in the default browser on failure or when `--open-report` is passed.

Shell e2e scripts are **retired** as scenarios migrate into the framework. Existing xUnit unit/handler tests remain; this framework complements them with full-stack regression.

### Report priorities (in order)

1. **Behavior diff** — committed git goldens; unintended observable changes surface as baseline diffs.
2. **Human operation transcript** — every test produces step-by-step reproduction instructions for reviewers and agents.
3. **Coverage discovery** — untested URLs are flagged and promoted into skeleton tests over time.

## User Stories

### Runner and local workflow

1. As a developer running regression locally, I want a **single command** (`dotnet run` on the E2E runner project) to exercise the full suite, so that I do not hunt for disparate shell scripts.
2. As a developer, I want the runner to **start and health-check Docker Compose** automatically, so that I do not forget to bring up storage nodes or dispatchers.
3. As a developer, I want to select a **compose profile** (fast default vs full HA), so that everyday regression is fast while replication and failure scenarios remain available.
4. As a developer, I want **`--update-baselines`** to regenerate committed golden files after intentional behavior changes, so that updating expectations is one documented step.
5. As a developer, I want **`--open-report`** to open the static HTML report in my browser on demand, so that I can inspect a passing run when debugging flakiness.
6. As a developer, I want the report to **open automatically on failure**, so that I immediately see transcript, diffs, and Playwright output without extra steps.
7. As a developer, I want **`--no-open-report`** for headless or future CI use, so that browser launch does not block automation.
8. As a developer, I want test reports written under a **local reports directory** (including a `latest` pointer), so that I can bookmark or share a run folder without committing it.
9. As a developer, I want **`--filter`** (or equivalent) to run a subset such as HA chaos or a single feature folder, so that I can iterate quickly during development.

### Human-readable transcripts

10. As a manual regression reviewer, I want each test to list **executed operations in plain language**, so that I can reproduce issues without reading C# or curl commands.
11. As an agent working in the repository, I want the same operation list in structured form, so that I can replay failures autonomously.
12. As a test author, I want **automatic logging** of HTTP method, URL, status, git command output, and similar wire details, so that I do not hand-write low-level steps.
13. As a test author, I want to add **short intent sentences** at logical boundaries (e.g. "Merge MR #7 as repository admin"), so that transcripts read like manual test cases rather than packet traces.

### Behavioral baselines and diffs

14. As a reviewer on a pull request, I want **committed baseline files in git** to change when observable behavior changes, so that unintended side effects appear in the diff alongside code.
15. As a reviewer, I want baselines stored in a **structured bundle** (operations transcript, API snapshots, page HTML, git state, side-channel records), so that I can inspect only the slice that changed.
16. As a reviewer, I want baselines to **normalize volatile values** (timestamps, run-specific slugs, user ids) via placeholder tokens, so that goldens remain stable across runs.
17. As a test author, I want baseline layout to **mirror the test's logical path**, so that I can navigate from a failing test to its golden files intuitively.
18. As a developer, I want **missing baselines to fail the run**, so that new tests cannot merge without reviewed expectations.
19. As a developer, I want baseline diffs to cover **API JSON responses** (status + normalized body), so that authorization and payload regressions are caught.
20. As a developer, I want baseline diffs to cover **normalized web page HTML** (stable DOM subset excluding live timestamps), so that UI content regressions are caught without pixel comparison in baselines.
21. As a developer, I want baseline diffs to cover **git state** (refs, commit SHAs, selected tree paths), so that push/merge/clone outcomes are verified.
22. As a developer, I want baseline diffs to cover **side-channel effects** (captured emails, in-app notification records via API where applicable), so that async outcomes are part of behavioral regression.

### Playwright integration

23. As a reviewer, I want **Playwright screenshots and traces** embedded in the unified static report, so that visual evidence sits beside behavioral diffs even though pixels are not committed baselines.
24. As a developer, I want the runner to invoke **existing TypeScript Playwright specs** without migrating them to C#, so that current visual spec investment is preserved.
25. As a developer, I want only Playwright specs **tagged for regression** (e.g. `@regression`) included in the default suite, so that designer-only visual specs are not run every time.
26. As a developer, I want Playwright execution gated in its **own tier after auth/API seed succeeds**, so that UI tests do not run when prerequisites failed.

### Git and storage integration

27. As a tester, I want to **push, clone, and fetch** via real git CLI over HTTPS through HAProxy and the dispatcher, so that Smart HTTP behavior matches production.
28. As a tester, I want git command **stdout/stderr captured in the transcript**, so that clone/push failures are diagnosable from the report.
29. As a tester, I want **LibGit2Sharp** used for read-only repo assertions after clone, so that commit and ref verification is reliable without fragile log parsing.
30. As a tester, I want scenarios migrated from **`e2e-https-git-test.sh`** (PAT create, push, clone, read-scoped denial) as first-class C# tests, so that the HTTPS git spine is always in the unified suite.
31. As a tester, I want **merge request lifecycle scenarios** migrated from shell e2e (protect branch, MR approvals, squash merge, discussion `closes` links), so that forge workflows regress atomically.
32. As a tester, I want **HA storage and replication scenarios** runnable under the full HA compose profile, so that quorum and replica behavior are regression-protected.

### HA chaos and failure injection

33. As a tester, I want **chaos helpers** to stop and restore compose services (e.g. a storage node, dispatcher, API replica) mid-suite, so that failure behavior is exercised realistically.
34. As a tester, I want chaos scenarios **auto-logged** ("Stopped storage-2 → push still succeeds with quorum"), so that failure tests remain human-readable.
35. As a tester, I want HA chaos tests tagged so I can **`--filter`** to them without running the full fast suite, so that HA debugging is targeted.
36. As a tester, I want the default fast profile to use **minimal replicas** (single storage, etc.), so that everyday regression completes in reasonable time.

### Test tiers and dependencies

37. As a tester, I want **Tier 0** (compose health, migrations) to **fail fast**, so that no feature tests run against a broken stack.
38. As a tester, I want **Tier 1** (auth seed, auth journey) to **fail fast**, so that downstream tests do not produce misleading failures when login is broken.
39. As a tester, I want **feature tiers** to **run all tests in the tier** even after an individual failure, so that the report shows full blast radius in one pass.
40. As a tester, I want **later tiers skipped** when an earlier tier failed, so that dependency semantics match real regression workflow.
41. As a tester, I want skipped tiers **recorded in the report** with reason, so that reviewers know what was not executed.

### Test data and identities

42. As a tester, I want each test to use a **unique namespace suffix**, so that parallel-safe isolation and baseline normalization are possible.
43. As a tester, I want the database **reset between tests** (truncate or equivalent), so that state does not leak across scenarios.
44. As a tester, I want a **seed tier** that creates core roles (admin, writer, outsider) via API for speed, so that feature tests do not repeat signup every time.
45. As a tester, I want a dedicated **auth journey suite** that exercises register → verification email → verify → login without debug shortcuts, so that the real onboarding path stays covered.
46. As a tester, I want seeded roles to allow **debug email verify** when verification is not the subject under test, so that seed setup stays fast.

### Email and third-party capture

47. As a tester, I want SendGrid replaced with an **in-memory capturing sender** when the E2E compose profile is active, so that no real email is sent during regression.
48. As a tester, I want captured emails to include **full HTML bodies**, so that verification links and codes can be parsed in auth journey tests.
49. As a tester, I want captured emails listed in the **transcript and baseline side-channel**, so that reviewers see "password reset email sent to …" with normalized content.
50. As a tester, I want a **test helper or internal read API** to query captured mail by recipient, so that tests can complete verification flows without Mailpit or external inboxes.

### Security and adversarial testing

51. As a security reviewer, I want an explicit **authorization matrix** per feature (anonymous, outsider, reader, writer, admin) with committed baselines for expected 401/403/404 responses, so that privilege regressions are visible in git diffs.
52. As a security reviewer, I want tests for **malformed and tampered requests** (missing auth, swapped tenant ids, invalid payloads) in curated scenarios, so that unauthorized access is regression-protected.
53. As a tester, I want an optional **`--fuzz` tier** that mutates valid requests and records outcomes **without committed baselines**, so that exploratory breakage is reported without golden churn.
54. As a security reviewer, I want **wrong error classes to fail the run** during fuzz (e.g. 500 when 403 was expected), so that server errors are not silently ignored.
55. As a reviewer, I want fuzz results in a **clearly marked report section** separate from baseline-gated tests, so that I distinguish deterministic regression from exploratory signal.

### URL discovery

56. As a tester, I want the framework to **extract links from visited pages** and compare against known test coverage, so that new UI routes are surfaced automatically.
57. As a tester, I want newly discovered URLs to **auto-generate skeleton C# tests** under a `Discovered` area, so that untested surface becomes grep-able and baseline-gated.
58. As a reviewer, I want discovery called out in the report (**new URL, no baseline yet**), so that coverage gaps are visible before promotion.
59. As a developer, I want auto-generated tests to **require `--update-baselines`** before they pass, so that new coverage is always reviewed.

### Static report website

60. As a reviewer, I want a **single self-contained static HTML site** per run with no CDN or npm dependencies at view time, so that reports work offline after unzipping.
61. As a reviewer, I want the report to combine **operation transcript, baseline diff highlights, full captured HTML pages, git operation output, email captures, and Playwright embeds**, so that one page answers "what happened."
62. As a reviewer, I want **pass/fail/skip status per test and per tier**, so that triage is scannable.
63. As a developer, I want the report generator to be testable in isolation, so that HTML output regressions are unit-testable without full compose.

### Migration and retirement

64. As a maintainer, I want **shell e2e scripts removed** once equivalent C# scenarios exist, so that regression entry points are not scattered.
65. As a maintainer, I want migration priority **git HTTPS → merge requests → HA chaos**, so that the most critical spine migrates first.
66. As a maintainer, I want existing **xUnit unit tests unchanged**, so that handler coverage and E2E regression coexist.

### Future CI (explicitly deferred)

67. As a future CI operator, I want the runner to support **`--no-open-report`** and a non-zero exit code on baseline or test failure, so that pipelines can adopt the same entry point later without GitHub-specific integration in v1.

## Implementation Decisions

### Architectural overview

The framework splits into **deep modules** with narrow public surfaces. The runner coordinates modules; xUnit test assemblies consume them through fixtures. No single module should know about HTML report layout, compose YAML, and baseline normalization simultaneously.

```
┌─────────────────────────────────────────────────────────────┐
│                     E2E Runner (CLI)                        │
│  compose profiles · tiers · flags · browser · exit codes    │
└──────────┬──────────────────────────────┬───────────────────┘
           │                              │
    ┌──────▼──────┐                ┌──────▼──────┐
    │   Compose   │                │  Playwright │
    │   Lifecycle │                │  Invoker    │
    └──────┬──────┘                └──────┬──────┘
           │                              │
    ┌──────▼──────────────────────────────▼──────┐
    │           Test Host (xUnit + fixtures)      │
    │  identities · db reset · steps · baselines  │
    └──────┬──────────┬──────────┬──────────┬──────┘
           │          │          │          │
     ┌─────▼────┐ ┌───▼───┐ ┌────▼────┐ ┌───▼────┐
     │ API/HTTP │ │  Git  │ │ Crawler │ │ Report │
     │  Client  │ │Facade │ │Discovery│ │  Gen   │
     └──────────┘ └───────┘ └─────────┘ └────────┘
```

### Module 1: E2E Runner (CLI host)

**Responsibility:** Single documented entry point. Parses CLI flags; orchestrates compose lifecycle; invokes xUnit test assembly; invokes Playwright subprocess; triggers report generation; opens browser; sets exit code.

**Public surface (conceptual):**

```
RunOptions {
  Profile: Fast | FullHa
  UpdateBaselines: bool
  OpenReport: OpenNever | OpenOnFailure | OpenAlways  // v1: OpenOnFailure default; OpenAlways via --open-report
  NoOpenReport: bool
  Fuzz: bool
  Filter: string?
}

RunResult {
  ExitCode: int
  ReportPath: string
  TierSummaries: TierSummary[]
}
```

**Behavior:**

- Default: fast compose profile, all tiers except fuzz, open browser only on failure.
- `--open-report`: open report even on success.
- `--no-open-report`: never open browser.
- `--update-baselines`: write/update golden files under the mirrored baseline layout; still emit report.
- `--fuzz`: append fuzz tier after UI tier.
- Delegates test execution to xUnit with assembly-level tier metadata consumed by the runner (collection ordering or explicit tier registry).

**Assumption:** The runner project is referenced by solution; developers run it via `dotnet run` on that project, not raw `dotnet test` on the test assembly alone (though the runner may host xUnit internally).

### Module 2: Compose Lifecycle Manager

**Responsibility:** Start/stop Docker Compose with profile overlays; wait for healthchecks; expose service endpoints to tests; support chaos operations.

**Public surface:**

```
IComposeEnvironment {
  Task StartAsync(ComposeProfile profile, CancellationToken ct)
  Task StopAsync(CancellationToken ct)
  Task WaitHealthyAsync(CancellationToken ct)
  Uri ApiBaseUrl { get; }
  Uri WebBaseUrl { get; }
  Uri GitHttpBaseUrl { get; }
}

IClusterChaos {
  Task StopServiceAsync(string serviceName, CancellationToken ct)
  Task StartServiceAsync(string serviceName, CancellationToken ct)
  Task RestoreAllAsync(CancellationToken ct)
}
```

**Profiles:**

- **Fast:** postgres, redis, minimal API/web/storage/dispatcher count sufficient for happy-path git and UI (exact service set defined during implementation to match existing compose capabilities).
- **FullHa:** full multi-storage, multi-dispatcher, multi-api topology from production-like compose for replication and chaos scenarios.

**Assumption:** Profile definitions live as compose override files or documented profile names, not ad-hoc `docker stop` of arbitrary containers outside the declared stack.

### Module 3: Tier Orchestrator

**Responsibility:** Execute tests grouped by tier; enforce fail-fast vs run-all semantics; skip later tiers on tier failure; record skip reasons for report.

**Tier model:**

| Tier | Name (conceptual) | Execution rule | On any failure |
|------|-------------------|----------------|----------------|
| 0 | Infrastructure | Ordered, fail-fast | Skip all higher tiers |
| 1 | Auth seed & journey | Ordered, fail-fast | Skip all higher tiers |
| 2+ | Feature domains | Run all tests in tier | Mark tier failed; skip higher tiers |
| UI | Playwright regression | After tier 1 passes | Skip fuzz if failed |
| Fuzz | Adversarial (optional) | After prior tiers | Fail run on wrong error class |

**Public surface:**

```
ITierRegistry {
  IReadOnlyList<TierDefinition> Tiers { get; }
}

TierDefinition {
  Id: int
  Name: string
  FailFast: bool
  TestCollections: string[]
}
```

Tests declare tier membership via attribute or collection name convention.

### Module 4: Operation Transcript (Step Logger)

**Responsibility:** Hybrid logging — automatic wire events plus human intent lines; export per-test transcript for report and baseline bundle.

**Public surface:**

```
IOperationTranscript {
  void Describe(string humanIntent, object? context = null)
  void RecordWire(WireEvent evt)  // called by wrappers, not test authors
  IReadOnlyList<TranscriptEntry> Entries { get; }
}

WireEvent kinds: HttpRequest, HttpResponse, GitCommand, GitOutput, EmailCaptured, ClusterAction, PlaywrightStep
```

**Normalization for baselines:** Transcript serialization replaces volatile tokens before comparison (`{{RUN_SUFFIX}}`, `{{USER_ID}}`, etc.) using rules from the isolation module.

### Module 5: Baseline Manager

**Responsibility:** Capture observable artifacts during test execution; normalize; compare to committed goldens; support update mode.

**Baseline bundle structure (per test):**

```
operations.json      # normalized transcript
api/{step-id}.json   # status, headers subset, normalized JSON body
pages/{step-id}.html # normalized HTML snapshot
git/{step-id}.txt    # refs, log excerpt, or structured git state
side-channel/
  emails/{step-id}.json
  notifications/{step-id}.json  # when asserted via API
```

**Public surface:**

```
IBaselineContext {
  Task CaptureApiAsync(string stepId, HttpCapture capture)
  Task CapturePageAsync(string stepId, string normalizedHtml)
  Task CaptureGitStateAsync(string stepId, GitStateSnapshot state)
  Task CaptureSideChannelAsync(string stepId, string channel, object payload)
  Task AssertMatchesCommittedAsync(CancellationToken ct)  // diff mode
  Task UpdateCommittedAsync(CancellationToken ct)         // --update-baselines
}
```

**Golden storage:** Committed under a root that mirrors test logical path (same relative path as test class/scenario). Reports never commit.

**Diff output:** Structured diff records consumed by report generator (field path, expected, actual, severity).

**Assumption:** Playwright pixel snapshots are **not** baseline inputs; they appear only in the HTML report.

### Module 6: Test Isolation & Identity Seeder

**Responsibility:** Per-test unique suffix; database reset; seed core identities for tier 1; expose role-based clients to tests.

**Public surface:**

```
ITestRunContext {
  string RunSuffix { get; }
  Task ResetDatabaseAsync(CancellationToken ct)
}

IIdentityFixture {
  Task SeedCoreRolesAsync(CancellationToken ct)  // admin, writer, outsider, etc.
  AuthenticatedClient AsAdmin { get; }
  AuthenticatedClient AsWriter { get; }
  AuthenticatedClient AsOutsider { get; }
  HttpClient Anonymous { get; }
}
```

**Auth journey tests:** Use public register API + captured verification email (full HTML parse) — no debug verify.

**Seed tier:** May call existing debug verify endpoints after optional registration when verification is not under test.

### Module 7: HTTP / API Test Client

**Responsibility:** Typed wrapper over HttpClient that auto-records wire events, attaches JWT/cookies, and integrates with baseline capture.

**Public surface:**

```
IApiClient {
  Task<HttpResult<T>> SendAsync<T>(ApiRequest request, CancellationToken ct)
}
```

Used by feature tests and security matrix tests. Supports raw malformed requests for adversarial scenarios (invalid JSON, wrong ids, missing auth).

### Module 8: Git Operations Facade

**Responsibility:** Execute git via system CLI for mutating operations; use LibGit2Sharp for assertions on cloned repositories; auto-log commands and output.

**Public surface:**

```
IGitOperations {
  Task PushAsync(GitRemote remote, string refSpec, CancellationToken ct)
  Task CloneAsync(GitRemote remote, string targetDir, CancellationToken ct)
  Task FetchAsync(GitRemote remote, CancellationToken ct)
}

IGitAssertions {
  GitStateSnapshot Inspect(string repoPath)
  void AssertCommitExists(GitStateSnapshot snap, string subject)
  void AssertRef(GitStateSnapshot snap, string refName, string? expectedSha = null)
}
```

Credentials follow existing PAT-over-HTTPS conventions through HAProxy.

### Module 9: Capturing Email Sender (API E2E profile)

**Responsibility:** Replace real SendGrid in E2E compose via configuration flag; store full outbound messages in memory; expose read API for tests.

**Implementation approach:**

- New implementation of existing `ISendGridEmailSender` that appends to an in-process store (singleton per API instance).
- E2E-only HTTP endpoint (admin/internal network or E2E flag gated) to list/clear captured messages filtered by recipient.
- Runner sets compose environment enabling capture mode.

**Captured message shape:**

```
CapturedEmail {
  To: string
  Subject: string
  HtmlBody: string
  SentAt: DateTimeOffset
}
```

Auth journey tests parse verification links or codes from `HtmlBody`. Baselines store normalized email summaries (subject, key links with tokens replaced).

**Assumption:** Reuses existing email send pipeline (`EmailSendQueryHandler` → `ISendGridEmailSender`); no Mailpit container in v1.

### Module 10: URL Discovery & Test Generator

**Responsibility:** During UI crawl or page capture steps, collect hrefs and API paths; diff against known coverage registry; emit skeleton test files.

**Public surface:**

```
ICoverageRegistry {
  bool IsCovered(string urlPattern)
  void RegisterDiscovered(string url, string sourceTest)
}

ITestGenerator {
  Task GenerateSkeletonAsync(DiscoveredUrl url, CancellationToken ct)
}
```

Generated tests land under a `Discovered` feature folder with marker comments indicating auto-generation date and source URL. They fail until `--update-baselines` creates goldens.

**Assumption:** v1 crawler is HTML link extraction from pages visited during tests plus OpenAPI route inventory optional stretch; full site spider is not required day one.

### Module 11: Playwright Invoker

**Responsibility:** Shell out to existing Nuxt Playwright CLI with tag filter; collect HTML report, screenshots, traces from Playwright output directory; hand artifacts to report generator.

**Behavior:**

- Runs in UI tier only if tier 1 succeeded.
- Tag convention: `@regression` (exact tag name finalized during implementation).
- Does not migrate specs to Playwright for .NET.

### Module 12: Fuzz Harness (optional tier)

**Responsibility:** Given a catalog of valid request templates, apply mutations (strip auth, swap ids, truncate body); classify response; fail on unexpected 5xx or wrong auth class.

**Public surface:**

```
IFuzzScenario {
  string Name { get; }
  ValidRequestTemplate Template { get; }
  ExpectedOutcome Expected { get; }  // e.g. Status403, Status401, Status404
}

IFuzzRunner {
  Task<FuzzResult[]> RunAsync(IEnumerable<IFuzzScenario> scenarios, CancellationToken ct)
}
```

Results append to report only; no committed baselines. Exit code reflects unexpected server errors and wrong status class.

### Module 13: Static Report Generator

**Responsibility:** Produce self-contained static site: index with tier summary, per-test pages with transcript, diff panels, embedded HTML iframes or sanitized sections, git logs, email viewers, Playwright screenshot gallery.

**Requirements:**

- All assets relative paths; no external network at view time.
- `latest` symlink or copy for browser open target.
- Unit-testable HTML builder independent of compose.

**Assumption:** No integration into OpenGitBase web UI or GitHub in v1; local filesystem only.

### Module 14: API application changes (minimal)

**Responsibility:** Wire capturing email sender when E2E flag set; expose captured-email query endpoint; ensure debug email verification endpoints remain available for seed tier.

**Configuration flag (conceptual):** `E2E__CaptureEmail=true` (exact naming aligned with existing `Debug__Features__*` patterns).

**Security:** Capturing endpoint disabled outside E2E/Development profile; not reachable in production configuration defaults.

### Test project organization

Single test assembly with feature-aligned folders (Repository, MergeRequest, Discussion, GitHttps, HaChaos, Security, AuthJourney, Discovered). Shared fixtures and helpers in a common folder within the same assembly or a referenced support library.

Runner project references test assembly and report/compose libraries.

### Migration map (shell scripts → C# scenarios)

| Retire | Replace with (conceptual) |
|--------|---------------------------|
| HTTPS git shell e2e | GitHttps tier: PAT push/clone/deny read-only push |
| Merge request shell e2e | MergeRequest tier: protect branch, MR lifecycle, closes links |
| HA storage shell e2e | HaChaos tier: full profile + quorum/failover assertions |
| Discussion/repo browse shell e2e | Discussion/Repository tiers as scenarios are ported |
| Storage layer shell integration | Remains until ported; not in unified runner v1 scope for Python tests |

### Assumptions recorded

- CI/CD pipeline integration is intentionally undefined in v1; runner API is CI-ready via flags only.
- In-app notification assertions use existing notification list APIs where available; not all notification channels may be baseline-gated in v1.
- Redis/internal job queue side effects are not baseline-gated in v1 unless exposed via API.
- Performance/time budgets are out of scope for v1.
- Multi-user concurrent scenarios (two browsers acting simultaneously) are out of scope for v1 unless already covered by API-level tests.
- OpenGitBase product UI will not host test reports in v1.

## Testing Decisions

### What makes a good test in this framework

- Assert **observable behavior** at system boundaries: HTTP status and body, rendered page content, git refs, captured email, and cluster behavior under chaos — not internal handler private state.
- Every scenario must produce a **human-readable transcript** sufficient for manual reproduction without reading test source.
- Baseline-gated tests must **fail on any normalized diff**, not only on explicit assert calls.
- Security matrix tests use **table-driven** cases across roles (anonymous, outsider, reader, writer, admin, owner).
- Chaos tests assert **documented HA semantics** from existing HA storage PRD (quorum write, read routing, promotion) at the git/API boundary.
- Fuzz tests assert **error class**, not exact body text.

### Modules to test (meta-level)

| Module | Test type | Prior art |
|--------|-----------|-----------|
| Baseline normalizer | Unit tests for token replacement | Existing test patterns in Common.Tests |
| Transcript serializer | Unit tests for wire + intent ordering | — |
| Report generator | Unit/snapshot tests for HTML structure | Playwright HTML reporter output |
| Baseline diff engine | Unit tests with fixture golden dirs | — |
| Test generator | Unit tests for skeleton file content | — |
| Capturing email sender | Unit tests; integration with EmailSendQueryHandler | `EmailSendQueryHandlerTests`, NSubstitute patterns for `ISendGridEmailSender` |
| Runner CLI | Integration smoke with mocked compose | — |
| End-to-end framework smoke | Single fast-profile test: health, seed, one API baseline | `e2e-https-git-test.sh`, `ControllerTestBase` |

### Framework scenarios (priority)

1. **Smoke:** Tier 0–1 pass; one API call with baseline; report generated.
2. **Git HTTPS:** PAT create, push, clone, read-scope push denied (replaces shell script).
3. **Auth journey:** Register → captured verification email → verify → login.
4. **Security matrix spot-check:** Anonymous cannot mutate protected resource (403/401).
5. **Discovery:** Visit page with unknown link → skeleton generated → fails without baseline.
6. **HA chaos (full profile):** Stop one storage node → assert documented push/read behavior.
7. **Playwright embed:** UI tier runs one `@regression` spec; screenshot appears in report.
8. **Fuzz (`--fuzz`):** One mutation returns 403; induced 500 fails run.

### Relationship to existing tests

- **xUnit handler/controller tests** remain the fast inner loop; no duplication of handler logic unless E2E scenario requires full stack.
- **Playwright visual specs** remain in TypeScript; framework invokes tagged subset only.
- **Shell e2e scripts** deprecated as C# scenarios land; delete scripts only after parity baselines committed.

### Out of scope for framework automated tests in v1

- Pixel/visual baselines inside committed goldens.
- Real SendGrid delivery or Mailpit infrastructure.
- Load/performance testing and SLA budgets.
- Concurrent multi-browser user simulations.
- Full OpenAPI fuzz of every endpoint (curated + optional fuzz tier only).

## Out of Scope

- **GitHub Actions / PR comment / artifact upload** integration in v1.
- **Hosting test reports inside OpenGitBase** web UI or admin pages.
- **Migrating Playwright specs to Playwright for .NET.**
- **Committing HTML report output** to the repository.
- **Replacing xUnit unit/handler tests** with E2E scenarios.
- **Property-based fuzz with committed baselines** (fuzz is report-only).
- **Mailpit/MailHog** as email capture mechanism (in-memory sender preferred).
- **Production instance testing**; framework targets local/staging compose stacks only in v1.
- **Automatic CI pipeline generation** (keep runner CI-agnostic until pipelines exist).

## Further Notes

### CLI flags (v1 target)

| Flag | Effect |
|------|--------|
| `--update-baselines` | Write/update committed golden bundles |
| `--open-report` | Open static report in default browser after run (even on success) |
| `--no-open-report` | Never open browser |
| `--fuzz` | Run optional fuzz tier |
| `--profile fast\|full-ha` | Select compose profile |
| `--filter <expr>` | Restrict tests (feature folder, trait, scenario name) |

Default browser behavior: open on failure only; no open on pass unless `--open-report`.

### Report vs baseline summary

| Artifact | Committed to git | Contents |
|----------|------------------|----------|
| Baseline bundle | Yes | Normalized API, HTML, git, side-channel, expected transcript |
| Static report site | No (local only) | Transcript, diffs, raw captures, Playwright embeds, tier skip reasons, discovery list |

### Decisions trace

This PRD consolidates design decisions from a structured planning session:

- Report priorities: behavior diff + human transcript (primary); URL discovery (secondary).
- Baselines: git-committed goldens only; structured bundles; no Playwright pixels in baselines.
- Runner: thin C# executable orchestrating compose, xUnit, Playwright TS, report, browser.
- Logging: hybrid auto-wire + human intent.
- Discovery: auto-generate skeleton tests; block without baselines; `--update-baselines` workflow.
- Security: curated auth matrix + optional fuzz tier (wrong error class fails).
- Environment: compose profiles; HA chaos helpers; tier orchestration with fail-fast seed tiers.
- Isolation: per-test suffix + DB reset; normalized placeholders in goldens.
- Email: capturing SendGrid replacement with full bodies; auth journey via captured mail; seed tier may use debug verify.
- Playwright: invoke existing TS specs tagged for regression in dedicated UI tier.
- Git: CLI for mutations; LibGit2Sharp for assertions.

### Suggested implementation order

1. Runner skeleton + compose fast profile + tier 0 smoke.
2. Operation transcript + baseline manager + report generator (minimal HTML).
3. Identity seeder + capturing email sender + auth journey test.
4. Git HTTPS scenario (shell script parity).
5. Playwright invoker + report embed.
6. URL discovery generator + Discovered folder convention.
7. Security matrix + fuzz tier.
8. Full HA profile + chaos helpers + HA scenarios.
9. Migrate merge request shell scenarios; retire superseded scripts.
