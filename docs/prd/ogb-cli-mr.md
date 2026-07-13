# PRD: `ogb mr` — Merge Request CLI Commands

## Problem Statement

The `ogb` CLI (v1) covers authentication and **issue-style workflows** mapped to Discussions, but it does not expose **merge requests** — the primary forge primitive for proposing, reviewing, and landing code changes. Developers and coding agents working from the terminal must open the web UI or craft raw HTTP calls against the Merge Request REST API to create MRs, inspect diffs, check merge eligibility, approve, or merge.

GitHub users expect **`gh pr`** as the terminal surface for the post-push loop: create a change proposal, read status and diff, wait for gates, merge. OpenGitBase already implements merge requests end-to-end in the API and web UI ([Merge Requests PRD](merge-requests.md)); the CLI simply has no commands yet.

Agents and local scripts benefit from the same ergonomics as `ogb issue`: repository context from git `origin`, `-R owner/repo` override, human tables by default, **`--json`** for structured parsing, and non-zero exit codes on failure. **Headless token login (`OGB_TOKEN`) is explicitly not part of this PRD** — authentication continues to use the existing loopback browser flow and OS credential store from `ogb auth login`.

## Solution

Add **`ogb mr`** (merge request) subcommands to the existing `OpenGitBase.Cli` project, backed by the **existing** `RepositoryMergeRequestsController` REST endpoints. No new domain entities or API routes are required.

Commands mirror the agent-relevant subset of **`gh pr`**, adapted to OpenGitBase terminology and lifecycle:

| Intent | `ogb mr` command | API backing |
|--------|------------------|-------------|
| Create proposal | `mr create` | `POST …/merge-requests` |
| List proposals | `mr list` | `GET …/merge-requests` |
| Show details | `mr view` | `GET …/merge-requests/{n}` |
| Lifecycle state only | `mr status` | `GET …/merge-requests/{n}` + mergeability |
| Unified diff | `mr diff` | `GET …/merge-requests/{n}/changes` |
| Publish draft | `mr ready` | `POST …/merge-requests/{n}/publish` |
| Approve | `mr approve` | `POST …/merge-requests/{n}/approve` |
| Merge | `mr merge` | `POST …/merge-requests/{n}/merge` |
| Abandon | `mr close` | `POST …/merge-requests/{n}/close` |
| Edit title/body | `mr edit` | `PATCH …/merge-requests/{n}` |

Global flags inherited from v1: `--hostname`, `-R owner/repo`, `--json`. Repository context uses the same `IGitRemoteResolver` and `-R` precedence as `ogb issue`.

Merge request lifecycle states exposed in CLI output use domain names: **Draft**, **Open**, **Approved**, **Merged**, **Closed** (not GitHub’s “open/closed/merged” simplification).

## User Stories

### Discovery and consistency

1. As a developer, I want `ogb mr --help` to list merge request subcommands and flags, so that I can discover MR capabilities alongside `ogb issue`.
2. As a developer, I want `ogb mr` commands to accept the same global flags as `ogb issue` (`--hostname`, `-R`, `--json`), so that scripting patterns stay consistent.
3. As a developer inside a git clone, I want `ogb mr` commands to infer repository coordinates from `origin` when `-R` is omitted, so that I can work from the repo directory without repeating owner/slug.
4. As a developer, I want clear errors when repository context cannot be inferred and `-R` was not provided, so that I know to pass `--repo` explicitly.

### Create

5. As a signed-in user who can push to a source branch, I want `ogb mr create --title "…" --head feature/foo --base main` to open a merge request, so that I can propose landing my branch from the terminal.
6. As a merge request author, I want `ogb mr create --body "…"` or `--body-file path` to include an optional Markdown description, so that reviewers have context in one step.
7. As a merge request author, I want `ogb mr create --draft` to create a **Draft** merge request, so that WIP work does not request review prematurely.
8. As a developer on a feature branch, I want `ogb mr create` to default `--head` to the **current git branch** when omitted (and `--base` to the repository default branch when omitted and discoverable), so that the common post-push flow matches `gh pr create`.
9. As a signed-in user, I want create to fail with API error semantics when source equals target, source has no commits ahead, or an active MR already exists for the pair, so that duplicate proposals are prevented.
10. As a signed-in user without create permission, I want create to fail with `401` / `403` matching the API, so that authorization stays consistent with the web UI.
11. As a developer, I want create success output to include MR number and web URL (`/{owner}/{slug}/merge-requests/{n}`), so that I can share or open the proposal.

### List

12. As a repository visitor with read access, I want `ogb mr list` to show merge requests for the current repository, so that I can triage from the terminal.
13. As a repository visitor, I want `ogb mr list --status draft|open|approved|merged|closed` to filter by lifecycle state, so that I can narrow results.
14. As a developer, I want list output to include number, title, status, source→target refs, and updated time at minimum, so that I can scan results quickly.
15. As a developer, I want `ogb mr list --json` to emit a stable array of merge request objects, so that agents can parse results without fragile text scraping.

### View

16. As a repository visitor, I want `ogb mr view 7` to show merge request metadata (title, body, status, refs, SHAs, approval counts, creator), so that I have full context without a browser.
17. As a developer, I want `ogb mr view 7 --json` to emit the full API-shaped merge request DTO, so that agents can consume structured fields (including identifier wrappers).

### Status and merge eligibility

18. As a developer, I want `ogb mr status 7` to print the lifecycle state and a concise mergeability summary, so that scripts can gate on whether an MR is ready to merge.
19. As a developer, I want `ogb mr status 7 --json` to include `status`, `isDraft`, `approvalCountAtHead`, `requiredApprovalCount`, and mergeability `status`/`message`, so that automation can branch without extra API calls.
20. As a developer waiting for human review, I want status to reflect **Approved** when merge gates pass, so that I know merge is allowed (pipeline gates remain a future extension).

### Diff and commits

21. As a repository visitor, I want `ogb mr diff 7` to show the unified diff for the merge request, so that I can review changes from the terminal (similar to `gh pr diff`).
22. As a developer, I want `ogb mr diff 7 --json` to emit the structured changes payload (files, hunks, lines), so that agents can summarize or inspect diffs programmatically.
23. As a repository visitor, I want `ogb mr view 7 --commits` (or `ogb mr commits 7`) to list commits on the MR, so that I can see the commit series without parsing diff output.

### Draft → Open

24. As a merge request author, I want `ogb mr ready 7` to publish a Draft to **Open**, so that reviewers know the proposal is ready (analogous to `gh pr ready`).
25. As a non-author, I want `ogb mr ready` to fail when the API denies publish, so that permissions match the web UI.

### Approve

26. As a Writer+ reviewer who is not the author, I want `ogb mr approve 7` to record my approval, so that I can sign off from the terminal.
27. As the merge request author, I want self-approval to fail with a clear API error, so that policy is enforced.
28. As a Reader, I want approve to fail with `403` / error detail, so that only eligible reviewers can approve.

### Merge

29. As a user with merge permission on the target branch, I want `ogb mr merge 7` to perform a server-side merge when the MR is **Approved**, so that I can land changes without the web UI.
30. As a merger, I want `ogb mr merge 7 --strategy merge-commit|squash|fast-forward` to select merge strategy when the repository policy allows, so that I can match team conventions.
31. As a merger, I want `ogb mr merge 7 --delete-branch` to delete the source branch after merge when requested, so that cleanup matches the web merge dialog.
32. As a developer, I want merge to fail with API error detail when the MR is not Approved, mergeability blocks merge, or permissions deny merge, so that failures are actionable.
33. As a developer, I want merge success output to confirm **Merged** status and print the merge commit SHA when available, so that scripts can verify the outcome.

### Close

34. As a merge request author, I want `ogb mr close 7` to close an Open or Approved MR without merging, so that I can abandon superseded work.
35. As a Writer+ member, I want to close MRs I did not author when the API allows, so that maintainers can clean up stale proposals.
36. As a developer, I want close to confirm **Closed** status in human and JSON output, so that scripts can verify the outcome.

### Edit metadata

37. As the merge request author, I want `ogb mr edit 7 --title "…"` and/or `--body "…"` / `--body-file` to update title and description, so that I can fix typos without the web UI.
38. As a Writer+ non-author, I want edit to succeed only when the API allows (same rules as PATCH endpoint), so that permissions stay consistent.

### Output, errors, and auth

39. As a developer, I want human-friendly tables and labels by default on list/status, so that interactive use is pleasant.
40. As a developer writing scripts, I want `--json` on all MR commands that produce data, so that I can pipe output to `jq`.
41. As a developer, I want JSON errors to include HTTP status and API error bodies when available, so that agents can log actionable failure details.
42. As a developer, I want non-zero exit codes on failure (auth, network, 4xx, 5xx), so that shell scripts and agent loops fail visibly.
43. As a developer whose JWT has expired, I want MR commands to fail with **"session expired — run `ogb auth login`"**, consistent with `ogb issue`.
44. As a developer using an interactive machine, I want to continue using `ogb auth login` (loopback browser) before MR commands — no token env var required for this PRD.

### Agent-oriented workflows (comparison to `gh pr`)

45. As a coding agent, I want `ogb mr create` after `git push` to open a tracked change proposal, so that my work enters the forge review loop without browser automation.
46. As a coding agent, I want `ogb mr diff` and `ogb mr status` to inspect my change and merge readiness, so that I can decide whether to fix, request review, or merge.
47. As a coding agent, I want `ogb mr merge` when policy allows, so that I can complete the loop without web UI clicks.
48. As a coding agent author, I want stable JSON field names across MR commands, so that toolchains do not break when human formatting tweaks.
49. As a coding agent operator, I accept that **non-interactive CI agents must run `ogb auth login` once on a machine with a browser** (or defer automation until a future token PRD), since token login is out of scope here.

## Implementation Decisions

### Product mapping

- **`ogb mr` is a CLI alias for Merge Requests** — no new domain entity. API and domain language remain **Merge Request** / `MergeRequestStatus`.
- Command group name is **`mr`** (not `pr`) to match OpenGitBase product vocabulary. A **`pr` alias** may be added later as a thin redirect to `mr` if desired; not required for v1 of this PRD.
- Lifecycle mapping for CLI output:

  | Domain state | CLI display |
  |--------------|-------------|
  | Draft | Draft |
  | Open | Open |
  | Approved | Approved |
  | Merged | Merged |
  | Closed | Closed |

- **`ogb mr ready`** maps to **publish** (Draft → Open), analogous to `gh pr ready`.
- **`ogb mr close`** maps to **close** (not “close as merged” — use `mr merge` for landing).
- **`ogb mr status`** combines MR lifecycle state with **mergeability** endpoint summary (similar to a lightweight `gh pr checks` before pipeline CLI exists).

### Comparison to `gh pr` (in scope vs deferred)

| `gh pr` capability | `ogb mr` v1 | Notes |
|--------------------|-------------|-------|
| create | ✓ `mr create` | Infer `--head` from current branch |
| list | ✓ `mr list` | Status filter via `--status` |
| view | ✓ `mr view` | |
| diff | ✓ `mr diff` | Structured JSON optional |
| status / checks | ◐ `mr status` | Mergeability + approvals; **no pipeline checks yet** |
| merge | ✓ `mr merge` | Strategy + delete-branch flags |
| close | ✓ `mr close` | |
| ready (undraft) | ✓ `mr ready` | |
| review / comment | ✗ | No MR comment REST API in v1 controller surface |
| checks --watch | ✗ | Deferred to pipeline CLI PRD |
| `--web` | ✗ | Print URL instead |

### Modules (extend existing CLI architecture)

Build on the deep modules established in [ogb CLI PRD](ogb-cli.md). Extend rather than duplicate.

#### 1. `IOgbApiClient` (extend)

Add merge request operations mirroring existing discussion methods:

- `ListMergeRequestsAsync(repo, status?)`
- `GetMergeRequestAsync(repo, number)`
- `CreateMergeRequestAsync(repo, request)`
- `UpdateMergeRequestAsync(repo, number, request)`
- `PublishMergeRequestAsync(repo, number)`
- `CloseMergeRequestAsync(repo, number)`
- `ApproveMergeRequestAsync(repo, number)`
- `GetMergeRequestChangesAsync(repo, number)`
- `ListMergeRequestCommitsAsync(repo, number)`
- `GetMergeRequestMergeabilityAsync(repo, number)`
- `MergeMergeRequestAsync(repo, number, request)`

Reuse **`FlexibleGuidJsonConverter`** (or equivalent) for identifier-wrapped JSON fields from API DTOs.

#### 2. `IGitBranchResolver` (new, small)

- **Responsibility:** Resolve current branch name from working directory (via `git rev-parse --abbrev-ref HEAD` or libgit2-style invocation).
- **Interface:** `TryGetCurrentBranch(out string branchName)`.
- **Used by:** `mr create` default `--head`.
- **Test double:** Returns fixed branch name in unit tests.

Optional follow-up: resolve default target branch via API (`GET repository/by-slug/...` or settings); v1 may require explicit `--base` when default cannot be resolved.

#### 3. `MergeRequestCommandHandlers` (new)

Thin handlers: validate flags → resolve repo context → call API client → write output → map exceptions to exit codes. Same pattern as `IssueCommandHandlers`.

#### 4. `IOutputWriter` (extend)

Add human + JSON formatters for:

- MR created (number, url, status)
- MR list rows
- MR view (metadata; optional commits section)
- MR status (state + mergeability one-liner)
- MR diff (human: patch-style or summary; JSON: API payload)
- MR merge/close/approve/ready confirmations

#### 5. Command router (`CliApp`)

Register `mr` command group with subcommands listed in Solution. Reuse `CliOptions` patterns (`TitleOption`, `BodyOption`, `BodyFileOption`, `RepoOption`, new `HeadOption`, `BaseOption`, `DraftOption`, `StrategyOption`, `DeleteBranchOption`, `StatusFilterOption`).

#### 6. URL builder

Extend repo URL helpers to produce merge request links: `{host}/{owner}/{slug}/merge-requests/{number}`.

### API endpoints consumed (existing)

| CLI action | HTTP |
|------------|------|
| List | `GET /repository/by-slug/{owner}/{slug}/merge-requests?status=` |
| View | `GET /repository/by-slug/{owner}/{slug}/merge-requests/{number}` |
| Create | `POST /repository/by-slug/{owner}/{slug}/merge-requests` |
| Edit | `PATCH /repository/by-slug/{owner}/{slug}/merge-requests/{number}` |
| Ready | `POST /repository/by-slug/{owner}/{slug}/merge-requests/{number}/publish` |
| Close | `POST /repository/by-slug/{owner}/{slug}/merge-requests/{number}/close` |
| Approve | `POST /repository/by-slug/{owner}/{slug}/merge-requests/{number}/approve` |
| Diff | `GET /repository/by-slug/{owner}/{slug}/merge-requests/{number}/changes` |
| Commits | `GET /repository/by-slug/{owner}/{slug}/merge-requests/{number}/commits` |
| Status (mergeability) | `GET /repository/by-slug/{owner}/{slug}/merge-requests/{number}/mergeability` |
| Merge | `POST /repository/by-slug/{owner}/{slug}/merge-requests/{number}/merge` |

Request/response bodies follow existing API models (`CreateMergeRequestRequest`, `UpdateMergeRequestRequest`, `MergeMergeRequestRequest`, `MergeRequestMergeabilityResponse`, etc.).

### Assumptions

- Users running `ogb mr` commands are already authenticated via `ogb auth login` (loopback JWT in OS credential store).
- Bearer JWT authorizes merge request endpoints the same way as the web SPA.
- Default production host serves API under `{host}/api/…` with path stripping at the edge (same as `ogb issue`).
- **MR review comments** and **discussion link** REST operations exist on the controller but are **out of scope** for v1 CLI (no general MR comment POST endpoint; links are a separate workflow).
- **Pipeline / CI status** is not merged into `mr status` until a pipeline CLI PRD exists; mergeability + approvals suffice for v1.
- Agents on headless CI without prior login will not be supported by this PRD (token auth explicitly deferred).

## Testing Decisions

### Principles

- Test **observable behavior**: HTTP request shapes, exit codes, stdout/stderr content, and JSON field stability — not `System.CommandLine` parsing internals.
- Prefer **in-memory doubles** for credential store and HTTP (`StubHttpMessageHandler`) in unit tests.
- Integration tests use in-process API (`WebApplicationFactory`) following `IssueCommandsIntegrationTests` patterns.
- Compose E2E adds `CliMrE2eTests` (or extends merge request tier) following `CliIssueE2eTests`.

### Modules to test

| Module | Focus | Prior art |
|--------|-------|-----------|
| `IOgbApiClient` MR methods | Paths, Bearer header, JSON deserialize (identifier wrappers), 401 → session expired | `OgbApiClientTests`, `FlexibleGuidJsonConverterTests` |
| `IGitBranchResolver` | Current branch detection; failure outside git repo | `GitRemoteResolverTests` |
| `MergeRequestCommandHandlers` | Flag validation; repo context errors; success output | `IssueCommandExtendedTests` |
| Output formatters | Human list/status; JSON snapshots | `IssueCommandExtendedTests`, integration tests |
| End-to-end CLI | create → view → status → close against in-process or compose API | `IssueCommandsIntegrationTests`, `CliIssueE2eTests` |

### Integration scenarios

- Authenticated CLI creates MR with `--head` / `--base`, receives number and URL.
- `mr list --status open` passes query param to API.
- `mr status` on Approved MR shows mergeable mergeability when API returns success.
- `mr merge` sends strategy and delete-branch flags in JSON body.
- Expired JWT → non-zero exit + re-login message.
- Temp git repo with `origin` → commands resolve owner/slug without `-R`.

### Compose E2E smoke

- Register user, push branch (or use existing MR fixture), `ogb mr create`, `ogb mr list`, `ogb mr view`, `ogb mr close` via `CliApp.RunAsync` against `localhost:8089/api`.

## Out of Scope

- **`OGB_TOKEN` / `ogb auth login --with-token`** — headless service authentication deferred (per product decision).
- **`ogb pr` alias** — optional ergonomics follow-up.
- **Pipeline / CI commands** (`ogb run`, `gh pr checks` equivalent) — separate PRD; not folded into `mr status` beyond mergeability.
- **MR review comments** (`gh pr comment`, `gh pr review`) — no dedicated MR comment POST API exposed in v1 controller inventory.
- **Discussion link commands** (`mr link`, `closes #12`) — API exists but deferred to keep v1 slice focused.
- **`branch-ahead-summary` standalone command** — may inform future `mr create` UX; not a standalone subcommand in v1.
- **Git push / clone / PAT management** — remain separate from `ogb`.
- **Web UI changes** — none required.
- **OAuth, device code, silent JWT refresh** — unchanged from ogb v1 auth PRD.

## Further Notes

### Relationship to ogb CLI v1

This PRD is a **vertical slice** on top of [ogb CLI PRD](ogb-cli.md). Reuse `CliApp`, `CliServices`, auth modules, host resolver, credential store, and output infrastructure. Remove “Merge request commands” from the v1 out-of-scope list once this ships.

### Suggested implementation order (tracer bullets)

1. **MR-01 — API client:** DTOs + `IOgbApiClient` merge request methods + unit tests.
2. **MR-02 — Branch resolver:** `IGitBranchResolver` + tests.
3. **MR-03 — Read commands:** `mr list`, `view`, `status`, `diff` + JSON output.
4. **MR-04 — Write commands:** `create`, `ready`, `approve`, `close`, `edit`, `merge`.
5. **MR-05 — Integration tests:** in-process API lifecycle (create → approve → merge or close).
6. **MR-06 — Compose E2E:** `CliMrE2eTests` smoke script optional (`scripts/test-ogb-cli-mr-e2e.sh`).

### Naming reference

| CLI (user-facing) | Domain / API |
|-----------------|--------------|
| mr | Merge Request |
| mr ready | publish (Draft → Open) |
| mr close | close (abandon) |
| mr merge | server-side merge (Approved → Merged) |
| mr status | MergeRequestStatus + mergeability |
| mr diff | changes (unified diff payload) |

### Agent workflow (target)

```text
git push origin feature/foo
ogb mr create --title "Add feature" --body-file /tmp/description.md
ogb mr diff 3 --json | jq …
ogb mr status 3 --json
# (human approves, or another terminal: ogb mr approve 3)
ogb mr merge 3 --delete-branch
ogb issue close 12   # if linked discussion exists (future link command)
```

Auth prerequisite on each machine: **`ogb auth login`** once via browser — acceptable for local agent loops; CI headless remains a future token PRD.
