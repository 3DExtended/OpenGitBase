# PRD: E2E Test Population (50+ Scenarios per Feature)

## Problem Statement

OpenGitBase now has a **unified E2E regression framework** (compose orchestration, human transcripts, committed behavioral baselines, tiered runner, Playwright integration). The framework spine is proven — infrastructure smoke, auth journey, HTTPS git, and thin coverage across discussions, merge requests, repository browse, and HA chaos.

However, **full-stack regression depth is far below product surface area**:

- Only **12 compose-backed E2E scenarios** exist today versus **~575 handler and API integration tests** and **dozens of PRD user stories per domain**.
- Retired shell e2e scripts documented **10+ critical paths per feature** (merge requests, discussions, repository browse, HA storage) that are only **partially** represented in C# E2E tests.
- Several product domains have **zero** E2E coverage: organizations, repository members, repository settings, admin replication, public discovery, SSH git.
- Without a deliberate population strategy, contributors will add ad-hoc tests inconsistently, baseline directories will sprawl, and full-suite runtime will become unmanageable.

Reviewers, release managers, and agents need a **systematic catalog of meaningful full-stack scenarios** — at least **50 per product feature** — that prove observable behavior across HTTP, git, storage, email, and (where appropriate) UI, without duplicating handler-level tests or producing filler assertions.

## Solution

Execute a **phased E2E population program** that grows the unified framework from ~12 to **~600 committed-baseline scenarios** organized into **12 product feature domains**, using a fixed **scenario taxonomy** (auth matrix, lifecycle, edge cases, cross-cutting side effects, chaos) and **tagged subsets** for daily smoke versus release regression.

The program:

1. **Hardens authoring infrastructure** (shared fixtures, scenario tags, runner filters, living scenario catalog) before bulk test writing.
2. **Closes documented parity gaps** from retired shell scripts and open issue specs (mr-16, disc-10, repo-browse-11, ha-storage-12).
3. **Expands authorization matrices** table-driven across roles (anonymous, outsider, reader, writer, admin, owner) per feature API surface.
4. **Mines existing integration tests** for promotion candidates where behavior crosses service boundaries (git, storage fleet, captured email).
5. **Splits runtime profiles**: fast compose for most scenarios; full-HA profile only for replication and quorum paths; Playwright behavioral specs complement API/git E2E where UI is the contract.
6. **Maintains a traceable scenario catalog** mapping each test to PRD user stories and parity issue acceptance criteria.

### Scale targets

| Tier | Scenarios | Purpose | Target runtime |
|------|----------:|---------|----------------|
| `@Smoke` | ~120 (~10/feature) | PR / daily dev gate | 15–25 minutes |
| `@Regression` | ~630 (~50+/feature) | Release / nightly | 3–6 hours (fast profile) |
| `@FullHa` | ~80 (subset) | HA replication proofs | +45–90 minutes |
| Playwright `@regression` | grows from 9 | UI shell + behavioral | separate tier |

### Feature domains (12)

| ID | Domain | Current E2E | Target |
|----|--------|------------:|-------:|
| F01 | Auth & account | 2 | 55 |
| F02 | Organizations | 0 | 52 |
| F03 | Repository lifecycle & settings | 0 | 54 |
| F04 | Repository members | 0 | 50 |
| F05 | Repository browse (content) | 1 | 52 |
| F06 | Discussions & sub-threads | 3 | 58 |
| F07 | Merge requests | 1 | 56 |
| F08 | Git HTTPS & PATs | 1 | 54 |
| F09 | SSH git (optional compose profile) | 0 | 50 |
| F10 | HA storage & replication | 1 | 52 |
| F11 | Admin fleet & replication | 0 | 50 |
| F12 | Public discovery & notifications | 0 | 50 |

### Scenario definition

One **scenario** equals one test method (or one theory data row with its own baseline scope) that:

- Asserts a **single primary observable outcome** (HTTP status + normalized body, git refs, captured email, notification record, or normalized HTML slice).
- Produces a **human operation transcript** with intent sentences.
- Commits a **baseline bundle** (operations, API snapshots, git state, side-channel records as applicable).
- Maps to at least one **PRD user story**, security invariant, or documented shell-parity step.

Scenarios that only repeat handler integration tests **without** crossing git, storage, email, or HAProxy boundaries are **out of scope** for E2E.

## User Stories

### Program governance and catalog

1. As a test architect, I want a **living scenario catalog** listing every E2E scenario with feature, status, PRD trace, and tags, so that coverage gaps are visible without reading the test assembly.
2. As a reviewer on a pull request that adds E2E tests, I want each new scenario to reference a **catalog row or PRD story ID**, so that additions are justified and not redundant.
3. As a developer, I want **quality gates** documented for what counts as a meaningful E2E scenario, so that the suite does not fill with low-signal duplicates.
4. As a release manager, I want a **coverage dashboard** in the static HTML report showing pass/fail/skip counts per feature domain, so that regression status is scannable at 600 scenarios.
5. As an agent implementing scenarios, I want the catalog to mark scenarios as `pending`, `in-progress`, or `done`, so that parallel work does not collide.

### Authoring infrastructure (Wave 0)

6. As a test author, I want **shared fixtures** for identity personas (admin, writer, outsider), repositories (public, private, empty), merge-request branches, PATs, organizations, and HA chaos helpers, so that I do not duplicate 40 lines of setup per test.
7. As a test author, I want fixtures to use **per-test run suffixes** for isolation without full database truncation between tests, so that parallel-safe naming matches existing framework design.
8. As a test author, I want **committed git testdata** (fixture repos with README, nested paths, oversized blob, SVG) seeded once per fixture class, so that browse and anchor scenarios are stable.
9. As a test author, I want a generic **auth matrix theory base** that runs HTTP cases across standard actors, so that 30+ scenarios per feature can be declared as data rows rather than copy-pasted methods.
10. As a developer, I want **`@Smoke` and `@Regression` traits** on every compose scenario, so that selective runs do not require brittle name filters.
11. As a developer, I want the runner to support **`--tag Smoke`** (and `--tag Regression`, `--tag FullHa`), so that daily iteration does not invoke the full 600-scenario suite.
12. As a developer, I want **`--feature Discussion`** (or equivalent) to run all scenarios in one domain folder, so that feature teams can own their slice.
13. As a test author, I want an **authoring checklist** in framework documentation (transcript, baseline, tag, catalog row, PRD link), so that new tests match conventions.
14. As a test author, I want **email capture helpers** wrapping the internal E2E mail API, so that auth and notification scenarios parse verification links consistently.
15. As a test author, I want **git operation helpers** (push, clone, assert refs via read-only library) centralized, so that HTTPS scenarios share push/clone semantics.

### Mining and discovery

16. As a test architect, I want a **promotion pipeline** from handler/API integration tests to E2E skeletons when tests assert cross-boundary behavior, so that existing ~575 tests inform scenario selection.
17. As a developer, I want **URL discovery** to continue generating skeleton tests for untested routes, so that browse and navigation gaps surface automatically.
18. As a reviewer, I want promoted skeletons to **fail until baselines are committed**, so that accidental merges of empty scenarios cannot occur.
19. As an agent, I want integration tests named with Unauthorized, Forbidden, NotFound, or HappyPath to be **indexed for E2E candidacy**, so that matrix expansion is systematic.

### Runtime and CI

20. As a developer running PR checks, I want **`@Smoke` E2E** to complete in under 30 minutes on a typical laptop, so that full-stack regression is practical on every change.
21. As a release engineer, I want **`@Regression` E2E** scheduled nightly with full compose bootstrap, so that deep coverage does not block every PR.
22. As a release engineer, I want **`@FullHa` scenarios** gated behind `--profile full-ha`, so that three-node tests do not run in the fast daily profile.
23. As a developer, I want **tier 7 (HA chaos)** to run only when the full-HA profile is selected, so that fast-profile runs do not fail on missing third-node semantics.
24. As a CI maintainer, I want **solution-wide `dotnet test`** to exclude compose E2E by default (unit/meta only in the E2E test project), so that unit CI does not require Docker.
25. As a developer, I want **Playwright tier 8** runnable in isolation via `--tier 8`, so that UI regression does not require compose for MSW visual specs.

### F01 — Auth & account (target 55)

26. As a tester, I want E2E coverage of **register → captured verification email → verify → login**, so that the real onboarding path stays protected (existing journey test maintained).
27. As a tester, I want **seeded personas** (admin, writer, outsider) created via API for downstream features, so that feature tests avoid repeated signup.
28. As a tester, I want **login failure** scenarios (wrong password, unverified account), so that auth errors remain stable in baselines.
29. As a tester, I want **password reset** flow with captured email and successful re-login, so that async email side effects are regression-protected.
30. As a tester, I want **change password** success and failure (wrong current password), so that account security mutations are covered.
31. As a tester, I want **account deletion** followed by denied login, so that lifecycle termination is observable.
32. As a tester, I want **resend verification** producing a second captured email, so that duplicate-send behavior is baseline-gated.
33. As a tester, I want **invite accept and decline** flows via token, so that organization onboarding paths are covered.
34. As a tester, I want **register validation** scenarios (duplicate email, weak password, missing fields), so that 400 responses are committed.
35. As a tester, I want **sign-out** invalidating subsequent API calls, so that session termination is proven at the HTTP boundary.
36. As a tester, I want **rate-limit smoke** on login endpoint (429 under burst), so that abuse protections regress visibly.
37. As a tester, I want **captured email baselines** for verify, reset, and invite templates (normalized HTML), so that template regressions appear in git diffs.

### F02 — Organizations (target 52)

38. As a tester, I want **create organization** and list my organizations, so that org CRUD happy paths are baseline-gated.
39. As a tester, I want **get organization by slug** for members and appropriate denial for outsiders, so that org visibility matches repository semantics.
40. As a tester, I want **add, promote, demote, and remove members** as owner, so that membership lifecycle is covered.
41. As a tester, I want **org invite create, resend, revoke, accept**, so that invitation flows match repository member patterns.
42. As a tester, I want **member cannot delete organization** and **last owner cannot leave**, so that safety invariants are enforced in E2E.
43. As a tester, I want **create repository under organization namespace**, so that org-owned repos integrate with git HTTPS scenarios.
44. As a tester, I want **auth matrix** on all org mutating endpoints for anonymous, outsider, member, and owner, so that privilege regressions are visible.

### F03 — Repository lifecycle & settings (target 54)

45. As a tester, I want **create public and private repositories** with metadata update, so that provisioning through storage fleet is exercised.
46. As a tester, I want **anonymous access** to public repo metadata and **404 on private**, so that discovery semantics match browse.
47. As a tester, I want **default branch** and **protected branch rule** configuration, so that merge request prerequisites are setup-able in E2E.
48. As a tester, I want **push rules** (forbidden paths, DCO requirement) enforced via git push denial with message substring, so that server-side rules match MR PRD.
49. As a tester, I want **repository deletion** removing browse and git access, so that cleanup is observable end-to-end.
50. As a tester, I want **usage/storage stats** endpoint returning plausible values after push, so that capacity signals integrate with storage nodes.
51. As a tester, I want **outsider denied** on delete and settings mutations, so that admin operations are matrix-covered.

### F04 — Repository members (target 50)

52. As a tester, I want **add list update remove collaborators** as owner, so that membership CRUD is baseline-gated.
53. As a tester, I want **role changes** (reader, writer, admin) affecting subsequent browse, git, and MR permissions, so that RBAC is proven cross-feature.
54. As a tester, I want **outsider denied** on member management endpoints, so that matrix cells match discussion and MR semantics.
55. As a tester, I want **new reader can clone private repo** after being added, so that membership changes propagate to git HTTPS.

### F05 — Repository browse (target 52)

56. As a tester, I want **public refs and tree** accessible anonymously (existing scenario maintained).
57. As a tester, I want **private tree 404 for anonymous and 403 for outsider, 200 for member**, so that repo-browse-11 private matrix is complete.
58. As a tester, I want **empty repository** returning empty refs without 500, so that new-repo UX is safe.
59. As a tester, I want **README endpoint** returning expected markdown for fixture repo, so that readme precedence is baseline-gated.
60. As a tester, I want **blob under 1MB** returned inline and **over 1MB** returning `isTooLarge` without inline body, so that size cap behavior is committed.
61. As a tester, I want **SVG blob** classified as download-only / non-inline preview, so that content-kind routing is regression-protected.
62. As a tester, I want **Cache-Control** `public` on public content and `no-store` on private, so that CDN semantics match PRD.
63. As a tester, I want **rate-limit smoke** (optional slow tier) returning 429 under anonymous burst, so that Redis limits are observable.
64. As a tester, I want **raw blob download** and **nested path navigation**, so that tree walking is covered beyond root.

### F06 — Discussions & sub-threads (target 58)

65. As a tester, I want **public anonymous read** and **create denied without auth** (existing scenario maintained).
66. As a tester, I want **private anonymous 404, outsider 403, member 200** (existing matrix maintained).
67. As a tester, I want **comment engages, resolve, reopen without re-engage** lifecycle, so that disc-10 lifecycle criteria are met.
68. As a tester, I want **blocked user reads but cannot comment; unblock restores write**, so that moderation controls are E2E-proven.
69. As a tester, I want **in-app notification** on comment for subscriber, so that async notification path is captured in baseline side-channel.
70. As a tester, I want **email subject** containing `[owner/repo #n]` prefix in captured mail, so that email templates regress visibly.
71. As a tester, I want **tag filter** on discussion list, so that disc-05 smoke is included.
72. As a tester, I want **anchored comment** returning located or outdated on fixture repo, so that code-anchor smoke is covered.
73. As a tester, I want **sub-thread create, reply, resolve** per discussion-sub-threads PRD, so that nested conversation depth is protected.
74. As a tester, I want **dismiss vs resolve** permission differences for writer+, so that lifecycle distinctions are not collapsed.
75. As a tester, I want **mention-triggered notification** and subscription opt-out, so that notification fan-out is covered.

### F07 — Merge requests (target 56)

76. As a tester, I want **protected main: writer push denied, feature branch push allowed** (existing scenario extended).
77. As a tester, I want **unprotected repo direct push still works**, so that MR remains optional when protection off.
78. As a tester, I want **admin allowlisted direct push** to protected ref, so that allowlist rules are E2E-proven.
79. As a tester, I want **Draft → publish Open → approvals → Approved → squash merge → Merged**, so that mr-16 core happy path is complete.
80. As a tester, I want **MR with `closes` link resolves discussion on merge**, so that cross-feature linking is regression-protected.
81. As a tester, I want **conflict when target advances** disabling merge until refresh, so that mergeability gates are observable.
82. As a tester, I want **public anonymous read MR; unauthenticated create 401**, so that public MR visibility matches code browse.
83. As a tester, I want **private anonymous 404; outsider 403**, so that MR auth matches discussion matrix.
84. As a tester, I want **force-push to MR source dismisses approvals**, so that approval stale semantics are covered.
85. As a tester, I want **duplicate MR** for same source→target rejected when open draft/approved exists, so that platform invariants are baseline-gated.
86. As a tester, I want **push rule rejection** with error message substring when DCO/path rules enabled, so that mr-16 scenario 4 is satisfied.

### F08 — Git HTTPS & PATs (target 54)

87. As a tester, I want **write PAT push and clone; read PAT push denied** (existing scenario maintained).
88. As a tester, I want **PAT create, list, revoke** API lifecycle, so that token management is baseline-gated.
89. As a tester, I want **revoked or invalid PAT** denied on git operations, so that credential revocation propagates to Smart HTTP.
90. As a tester, I want **org-owned repository HTTPS push**, so that integration-test-https-git-org-repo parity is in unified runner.
91. As a tester, I want **push to protected branch denied** linking to F07 rules, so that git and API enforcement align.
92. As a tester, I want **access-check API matrix** per repository role, so that dispatcher validation is visible without SSH.
93. As a tester, I want **fetch after push** updating refs on clone, so that git protocol completeness is proven.
94. As a tester, I want **git transcript** capturing stdout/stderr on failure paths, so that clone/push errors are diagnosable from report.

### F09 — SSH git (target 50, optional profile)

95. As a tester, I want **SSH public key CRUD** and fingerprint lookup, so that SSH auth path is baseline-gated when SSH compose profile enabled.
96. As a tester, I want **clone and push over SSH** through dispatcher, so that git-storage-proxy parity is in unified suite.
97. As a tester, I want **unauthorized key rejected** on git SSH, so that security matrix extends to SSH edge.
98. As a tester, I want SSH scenarios **skipped by default** unless SSH compose profile is active, so that fast profile stays lean.

### F10 — HA storage & replication (target 52)

99. As a tester, I want **RF=3 bare repos on three nodes** after repository create under full-HA profile, so that ha-storage-12 provisioning criterion is met.
100. As a tester, I want **push incrementing watermarks** on at least two nodes, so that quorum write path is proven.
101. As a tester, I want **clone/fetch via read replica** or read-routing metadata verification, so that read path HA is regression-protected.
102. As a tester, I want **primary storage stop → promotion → push resumes**, so that failover behavior matches HA PRD.
103. As a tester, I want **non-primary node stop → quorum 2/3 push succeeds**, so that partial fleet degradation is covered.
104. As a tester, I want **repository delete with one node down** completing DB and disk cleanup, so that quorum delete is proven.
105. As a tester, I want **stop storage node; API health remains** (existing chaos scenario maintained).
106. As a tester, I want **rebalance when node returns** (stretch), so that fleet self-healing is observable where harness supports it.

### F11 — Admin fleet & replication (target 50)

107. As a tester, I want **admin-only access** to fleet and replication admin APIs with outsider denied, so that privilege boundaries are matrix-covered.
108. As a tester, I want **storage node list and per-repo replication summary** matching fleet state after push, so that admin-replication-ui API contract is E2E-proven.
109. As a tester, I want **replication attention flags** (degraded, missing replica) surfaced for injected failure fixtures, so that operator signals regress visibly.
110. As a tester, I want **Playwright smoke** that admin replication page renders against seeded API (optional UI tier), so that admin UI is not only API-tested.

### F12 — Public discovery & notifications (target 50)

111. As a tester, I want **public repository discovery** and **recent repositories** endpoints returning expected shapes, so that logged-out explore data is baseline-gated.
112. As a tester, I want **owner profile public page** data consistent with API, so that discovery and profile routes align.
113. As a tester, I want **notifications list, unread count, mark read**, so that in-app notification API is covered cross-feature.
114. As a tester, I want **MR and discussion events** producing notification records inspectable via API, so that fan-out from F06/F07 is E2E-visible.

### Baselines, transcripts, and reports at scale

115. As a reviewer, I want **one baseline bundle per scenario method**, so that diffs remain navigable at 600 scenarios.
116. As a reviewer, I want **volatile fields normalized** consistently as new domains add ids (org ids, MR numbers, notification ids), so that golden churn stays low.
117. As a manual regression reviewer, I want **feature-level summary** in HTML report, so that I do not scroll 600 individual sections blindly.
118. As a test author, I want **missing baseline to fail** when adding scenarios, so that `--update-baselines` is an explicit review step.

### Phasing and ownership

119. As a project lead, I want **Wave 1** to close shell-parity gaps before matrix expansion, so that documented user pain is addressed first.
120. As a project lead, I want **Wave 2** auth-matrix theories to bulk-fill scenarios without 500 copy-pasted methods, so that 50/feature is achievable.
121. As a feature team owner, I want **clear ownership** per feature folder in the E2E test assembly, so that reviews route to domain experts.
122. As a contributor, I want **smoke scenarios** defined first per feature before regression depth, so that incremental value ships every PR.

## Implementation Decisions

### Relationship to existing E2E framework PRD

This PRD **extends** the unified E2E regression framework (compose runner, tiers, baselines, transcripts, Playwright invoker). It does not replace framework mechanics. All new scenarios **must** use existing patterns: `BeginScenario()`, `E2eApiClient`, `BaselineManager`, `RequiresComposeFact`, tier attributes, and runner entry point.

**Assumption:** Framework stabilization (staged fleet bootstrap, compose health skip, Playwright report embed, `--tier`, solution test isolation) is complete on `main` before Wave 1 bulk authoring begins.

### Major modules to build or extend

#### 1. Scenario catalog (new)

**Responsibility:** Living index of all E2E scenarios with feature id, name, status, tags, PRD story references, and parity issue links.

**Interface:** Markdown table or generated JSON consumed by report generator for feature summaries. Updated in the same PR as new tests.

**Does not:** Execute tests or store baselines.

#### 2. Fixture library (extend E2E core)

**Responsibility:** Encapsulate multi-step setup (users, repos, orgs, branches, PATs, discussion seeds, HA probes) behind stable fluent builders returning authenticated clients and resource ids/slugs.

**Interface:** Async factory methods parameterized by run suffix; idempotent within a test; no cross-test shared mutable state.

**Does not:** Replace identity seed tier — complements it with feature-specific bundles.

#### 3. Auth matrix theory runner (new)

**Responsibility:** Table-driven HTTP scenarios: actor × method × path × expected status class × optional baseline capture name.

**Interface:** Declarative case records; integrates with transcript and baseline manager; skips inapplicable cells (e.g., anonymous cannot call owner-only endpoint — still assert 401).

**Does not:** Replace dedicated journey tests (register → email → verify).

#### 4. Git testdata provisioning (new)

**Responsibility:** One-time or per-fixture-class seed of repositories with known file tree (README, nested paths, >1MB blob, SVG, anchor targets) via git push in setup.

**Interface:** Invoked from browse, discussion anchor, and MR diff scenarios.

**Does not:** Commit large blobs to baselines — only refs and API responses.

#### 5. Runner tag and feature filters (extend)

**Responsibility:** Map `--tag Smoke` to xUnit trait filter; map `--feature Organizations` to namespace/category filter; compose with existing `--filter` and `--tier`.

**Interface:** CLI flags documented alongside existing runner options.

**Does not:** Change tier fail-fast semantics for tiers 0–1.

#### 6. Report feature rollup (extend)

**Responsibility:** Aggregate tier summaries and per-feature pass/fail/skip in static HTML index.

**Interface:** Consumes scenario catalog metadata and tier results.

#### 7. Integration-test promotion indexer (optional tooling)

**Responsibility:** Scan test assemblies for naming patterns and cross-boundary markers; output candidate list for catalog `pending` rows.

**Interface:** CLI or one-off script; human promotes to E2E.

**Does not:** Auto-generate baselines without review.

#### 8. Per-feature test assembly organization (extend)

**Responsibility:** Group scenarios under feature folders with `Fixtures/`, `Scenarios/`, `Theories/` subfolders; one xUnit category trait per feature matching tier registry.

**Interface:** Aligns with existing `Category=Discussion` filter pattern.

### Scenario taxonomy (how 50+ per feature is achieved without filler)

Each feature draws scenarios from up to five buckets:

| Bucket | Typical count | Example |
|--------|--------------|---------|
| **A. Shell/PRD parity** | 8–15 | mr-16 scenarios 1–10 |
| **B. Auth matrix** | 15–25 | 6 actors × 4–5 mutating endpoints |
| **C. Lifecycle/state machine** | 8–12 | MR Draft→Merged; Discussion Open→Resolved |
| **D. Edge/validation** | 8–12 | Duplicate slug, empty repo, revoked PAT |
| **E. Cross-cutting side channel** | 5–10 | Email, notification, git refs, watermark |

Buckets A+C are authored as explicit methods; bucket B uses theories; buckets D+E mixed.

### Test data and isolation

- Continue **per-test run suffix** normalization — no full database truncate between tests (established stabilization decision).
- **Email capture** via in-memory sender and internal E2E mail API for auth and notification scenarios.
- **HA chaos** uses existing cluster stop/start helpers; full-HA profile required for quorum assertions.
- **SSH scenarios** gated on optional compose profile; default skip with recorded reason in report.

### Phased delivery

| Wave | Focus | Approx. scenarios added |
|------|-------|------------------------|
| **0** | Fixtures, tags, runner filters, catalog, matrix base | 0 (enablers) |
| **1** | Shell parity: MR, discussion, browse, HA git gaps | +35 |
| **2** | Auth matrix theories per feature F01–F08 | +250 |
| **3** | F02, F03, F04, F12 greenfield | +150 |
| **4** | F09, F10 full-HA, F11 admin | +120 |
| **5** | Playwright behavioral specs (non-pixel) | +40 UI |

### Traceability

- Catalog columns: `id`, `feature`, `name`, `tags`, `prd_stories[]`, `parity_issue`, `status`, `owner`.
- IDs suggested format: `E2E-F07-012` for merge request scenario 12.

## Testing Decisions

### What makes a good E2E scenario in this program

- Assert **observable system behavior** at boundaries: HTTP status and normalized JSON, git refs after clone/push, captured email HTML, notification API records, storage watermark metadata — not private handler state.
- Every scenario produces a **human transcript** sufficient to reproduce without reading C#.
- Every `@Regression` scenario has a **committed baseline bundle**; missing baselines fail the run.
- Prefer **cross-service proofs** over repeating handler tests — promote only when git, storage, email, HAProxy, or fleet participation is required.
- **Auth matrix** scenarios assert exact expected status class (401 vs 403 vs 404) per OpenGitBase visibility conventions (private resources return 404 to anonymous).
- **Chaos scenarios** document injected failure in transcript before assertion.
- **Fuzz tier** remains separate — no committed baselines; not counted toward 50/feature target.

### Modules to test

| Module | Test type | Prior art |
|--------|-----------|-----------|
| Fixture library | Unit tests for builder outputs (mock HTTP) | Existing `E2eScenarioHelpers`, identity seed tests |
| Auth matrix runner | Unit test for case filtering and skip logic | `AuthMatrixTests` theory pattern |
| Scenario catalog | Lint check: every `[Regression]` test has catalog row | — |
| Runner tag filter | Smoke integration: `--tag Smoke` runs subset | Existing `RunOptions` tests |
| Report feature rollup | Unit test HTML contains feature table | `ReportGeneratorTests` |
| Each new scenario | Compose E2E with baselines | All existing `*E2eTests` classes |
| Promotion indexer | Optional unit tests on pattern matching | `TestGenerator`, URL discovery tests |

### Prior art for scenario content

| Domain | Issue / script parity | Existing integration depth |
|--------|----------------------|---------------------------|
| Merge requests | mr-16 (10 scenarios) | `OpenGitBase.Features.MergeRequest.Tests` (~26), API MR controllers (~44) |
| Discussions | disc-10 (7 scenarios) | `OpenGitBase.Features.Discussion.Tests` (~36) |
| Browse | repo-browse-11 (9 criteria) | API repository content (~79) |
| HA storage | ha-storage-12 (7 criteria) | `OpenGitBase.Features.StorageNode.Tests` (~23) |
| Git HTTPS | e2e-https-git-test.sh | `OpenGitBase.Features.GitAccessToken.Tests` (~13), Dispatcher tests (~9) |
| Auth | e2e-08 journey | API auth controllers (~70) |

### CI recommendations (documentation only in v1)

| Job | Scope | Command pattern |
|-----|-------|-----------------|
| PR smoke | `@Smoke` | Runner with `--tag Smoke --no-open-report` |
| Nightly regression | `@Regression` fast profile | Full runner |
| Nightly HA | `@FullHa` | Runner `--profile full-ha --tag FullHa` |
| UI | Playwright tier | Runner `--tier 8` |
| Unit | All non-compose | `dotnet test OpenGitBase.sln` |

**Assumption:** GitHub Actions wiring is out of scope for this PRD; commands are documented for manual and future CI adoption per parent framework PRD.

## Out of Scope

- **Replacing or deleting** existing handler/API integration tests — E2E complements them.
- **Pixel-based baselines** in git for UI — Playwright screenshots remain report-only.
- **Property-based fuzz with committed goldens** — fuzz tier stays exploratory.
- **Production/staging environment testing** — local compose only.
- **Automatic CI pipeline implementation** in this PRD phase.
- **50 scenarios for internal/infrastructure-only surfaces** (health, migrations) — Tier 0 stays minimal.
- **Performance/load testing** beyond optional rate-limit smoke.
- **Migrating Playwright specs to C#**.
- **Mailpit/MailHog** — continue in-memory email capture.
- **Achieving 600 scenarios in a single release** — phased waves over multiple milestones.

## Further Notes

### Dependency on parent PRD

Implementation assumes the [Unified E2E Regression Framework](./e2e-regression-framework.md) runner, baseline layout, tier orchestration, and compose profiles remain the execution backbone. Population work adds **content and authoring infrastructure**, not a second test framework.

### Catalog location (proposed)

Living backlog: `docs/e2e/scenario-catalog.md` (to be created in Wave 0). Feature teams update rows in the same PR as scenario implementations.

### Smoke-first rule

Each feature must define **10 `@Smoke` scenarios** before expanding to full `@Regression` depth. Smoke set must include: one happy path, one auth denial, one private-visibility check (where applicable), and one side-channel or git proof (where applicable).

### Quality rejection criteria

Reject scenarios that: duplicate handler-only coverage; assert only status code without baseline; depend on execution order; lack catalog row; lack PRD or parity trace.

### Estimated totals

| Metric | Value |
|--------|------:|
| Feature domains | 12 |
| `@Regression` scenarios target | ~630 |
| `@Smoke` scenarios target | ~120 |
| Current compose E2E | 12 |
| Gap to close | ~618 |

### Open assumptions

- Organization and SSH features remain product-supported; if SSH stays disabled by default, F09 scenarios are tagged `@FullHa` or `@SshProfile` and excluded from default regression counts until profile is standard in dev docs.
- Sub-thread discussion scenarios depend on discussion-sub-threads feature completeness on `main`.
- Admin UI Playwright coverage is optional smoke; API-level admin scenarios satisfy minimum F11 depth.
