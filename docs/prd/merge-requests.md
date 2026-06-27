# PRD: Merge Requests (Branch Protection, Review, and Server-Side Merge)

## Problem Statement

OpenGitBase provides repository hosting, role-based git access, web code browsing, and repository discussions — but there is no structured way to propose, review, and land code changes through the product. Any user with **Writer** access can push directly to any branch, including the default branch. There is no branch protection, no required review, no push policy enforcement, and no server-side merge workflow.

Developers expect a forge-style **merge request** experience: push a feature branch, open a tracked change proposal against a target branch, collect feedback and approvals on a diff, and merge through the platform when policy is satisfied. Maintainers need **protected branches** with configurable bypass lists (by role or named user), **force-push rules**, and **push rules** (commit message patterns, forbidden paths, DCO) enforced at the git layer — not only in the web UI.

The repository discussions PRD deliberately deferred merge-request-style change proposals, linking commits to discussions, and approval flows. Discussions remain the place for bugs, design questions, and general threads. This PRD defines **Merge Requests** as a separate collaboration surface that optionally links to discussions (including **`closes`** links that resolve discussions on merge) while reusing shared presentation infrastructure (Markdown editor, rendered code blocks, comment thread layout, git anchor attach flow, mentions, notifications).

A subsequent **CI/CD pipelines** feature will depend on the **Approved** merge-request state as a stable gate hook. This PRD designs approvals and merge eligibility so pipeline status checks can plug in without renaming states or reshaping the merge-request model.

## Solution

Add **merge requests**: per-repository, sequentially numbered change proposals (`/{owner}/{repo}/merge-requests/7`) that pair a **source branch** with a **target branch** within the same repository. Merge requests track source and target HEAD SHAs, support a Markdown description, optional links to discussions, general comments, and line-level **review comments** on a unified diff. The UI reuses discussion-era components (Markdown editor, rendered body/code blocks, sub-thread layout, resolve/collapse behavior, code-attach modal) inside merge-request-specific page shells.

Each merge request follows a five-state lifecycle:

```
Draft → Open → Approved → Merged
              ↘ Closed (abandoned / rejected; any time before merge)
```

- **Draft** — visible to repository readers; not ready for review; cannot be approved or merged.
- **Open** — under review; collecting feedback, human approvals, and (in a future CI/CD feature) pipeline results.
- **Approved** — all configured **merge gates** satisfied at the current source HEAD. In v1, gates are human approvals only; pipeline gates register later without changing this state.
- **Merged** — platform performed a server-side merge onto the target branch using a selected merge strategy.
- **Closed** — abandoned or rejected without merging.

**Protected branch rules** (configured per repository in settings) match branch name patterns (including `@default` alias) and control: direct push allowance (default deny except platform merge identity and configured allowlists), required approval count, who may merge, force-push policy, optional locked merge strategy, push-rule sets, and whether new commits dismiss existing approvals.

**Push rules** enforce max file size, forbidden path globs, optional commit-message regex, and **Signed-off-by (DCO)** on every commit in a push. Enforcement occurs at the git layer (`WriteGit` access checks and storage receive-pack validation) before objects land.

**Review model:** merge-request review comments are stored separately from discussion comments but expose DTO shapes compatible with shared frontend thread components. Overview comments are free-floating; **Changes** tab comments anchor to `(headCommitSha, filePath, lineNumber, diffSide)` with sub-thread replies and per-root resolve semantics mirroring discussion sub-threads. Outdated anchors collapse when the diff changes.

**Discussion cross-links** support relationship types `closes`, `related`, and `implements`. Merging a merge request with `closes` links automatically transitions linked discussions to **Resolved**.

## User Stories

### Discovery and access

1. As a repository visitor with read access, I want to see a **Merge requests** list for the repository, so that I can find open and historical change proposals.
2. As a repository visitor, I want merge request URLs to use a per-repository sequential number (`/merge-requests/7`), so that links are human-friendly and stable when titles change.
3. As an anonymous visitor on a **public** repository, I want to read merge requests and their comments, so that I can follow development without an account.
4. As an anonymous visitor, I want to be required to sign in before creating, commenting on, or approving merge requests, so that participation is attributable.
5. As a signed-in user without read access to a private repository, I want merge requests to be inaccessible with the same semantics as private code browse (**404** for anonymous, **403** for authenticated outsiders), so that permissions stay consistent.
6. As a repository visitor, I want the merge request list sorted by **recently updated** by default, so that active proposals surface first.
7. As a repository visitor, I want to filter merge requests by status (Draft, Open, Approved, Merged, Closed), author, target branch, and label defer, so that I can triage what matters.

### Creating merge requests

8. As a user who can push to a source branch, I want to create a merge request by choosing source and target branches, so that I can propose landing my work.
9. As a merge request author, I want the target branch to default to the repository **default branch**, so that the common case is low friction.
10. As a merge request author, I want to provide a title and optional Markdown description, so that reviewers have context.
11. As a merge request author, I want to start a merge request as a **Draft**, so that I can push work-in-progress without requesting review.
12. As a merge request author, I want to link discussions by number (`#12`) with relationship types `closes`, `related`, or `implements`, so that work traces back to conversations.
13. As a platform, I want to reject creating a merge request when source equals target, when source has no commits ahead of target, or when an **Open**, **Draft**, or **Approved** merge request already exists for the same source→target pair, so that duplicates are avoided.
14. As a user who cannot push to the source branch, I want merge request creation denied, so that only branch contributors can open proposals for that branch.
15. As a merge request author, I want a **Create merge request** banner after pushing a branch with commits ahead of the default branch, so that I discover the next step without auto-created noise.
16. As a repository visitor, I want **New merge request** and **Compare branches** entry points in the repository navigation, so that I can start a proposal deliberately.

### Merge request lifecycle

17. As a merge request author, I want to publish a Draft to **Open**, so that reviewers know the proposal is ready.
18. As a merge request author, I want to close my Open or Approved merge request without merging, so that I can abandon superseded work.
19. As a Writer+ member, I want to close any merge request, so that maintainers can clean up stale proposals.
20. As a platform, I want **Open → Approved** to occur automatically when all merge gates pass at the current source HEAD, so that no separate “submit for approval” action is required.
21. As a platform, I want **Merged** to be reachable only from **Approved**, so that merge eligibility is explicit.
22. As a merge request author, I want source and target HEAD SHAs tracked and updated when branches move, so that reviewers always see current commits.
23. As a repository visitor, I want clear status badges (Draft, Open, Approved, Merged, Closed) styled consistently with discussion status badges, so that state is scannable.

### Approvals and merge gates

24. As a Writer+ repository member who is not the merge request author, I want to **Approve** an Open merge request, so that I can sign off on landing the change.
25. As the merge request author, I want to be prevented from approving my own merge request, so that self-approval cannot satisfy policy.
26. As a Reader, I want to be prevented from approving, so that approvals reflect write-capable reviewers.
27. As a protected-branch maintainer, I want to configure a **required approval count** per rule (e.g. `main` requires 2), so that high-trust branches get multiple sign-offs.
28. As a merge request author, I want all approvals **dismissed when new commits are pushed** to the source branch (when the rule enables this, on by default), so that reviewers re-evaluate changed code.
29. As a merge request author, I want force-pushes to the source branch to dismiss approvals the same as new commits, so that rewritten history cannot inherit stale sign-off.
30. As a platform, I want Draft merge requests to be unapprovable, so that WIP work does not accumulate approvals.
31. As a future pipeline author, I want **Approved** to mean “all merge gates satisfied,” so that CI/CD can register additional gates without introducing a new merge-request status.

### Protected branches

32. As an Admin or Owner, I want to configure **protected branch rules** matching branch patterns (`main`, `release/*`, `@default`), so that important refs are guarded.
33. As an Admin or Owner, I want protected branches to **block direct pushes by default**, so that changes land only through merge requests or explicit allowlists.
34. As an Admin or Owner, I want to allow direct pushes by **role groups** (Owner, Admin/Maintainer, Writer) and by **named repository members**, so that trusted actors can bypass in emergencies.
35. As an Admin or Owner, I want the **platform merge identity** always allowed to update protected refs during server-side merges, so that the merge path remains functional.
36. As an Admin or Owner, I want to require merge requests for merging into protected branches, so that review policy is enforceable.
37. As an Admin or Owner, I want to configure who may **merge** approved merge requests (default Writer+), so that merge authority is controlled.
38. As an Admin or Owner, I want per-rule **force-push policy** (deny all, allow allowlisted pushers, platform only on target), so that history rewriting is controlled.
39. As an Admin or Owner, I want no branches protected until I opt in, so that new repositories stay frictionless.
40. As an Admin or Owner, I want to protect `@default` even before the default branch exists on disk, so that policy is ready when the first push creates it.

### Push rules

41. As an Admin or Owner, I want **push rules** enforced on git push, so that policy violations fail before objects land.
42. As an Admin or Owner, I want to reject pushes containing files matching **forbidden path globs** (e.g. `*.pem`, `.env`), so that secrets and artifacts do not enter the repository.
43. As an Admin or Owner, I want to enforce a **maximum file size** per blob in a push, so that oversized files are rejected consistently with storage quota posture.
44. As an Admin or Owner, I want an optional **commit message regex** per rule, so that teams can enforce conventions (e.g. Conventional Commits).
45. As an Admin or Owner, I want every commit in a push to include **Signed-off-by (DCO)**, so that contribution attestation matches project CONTRIBUTING expectations.
46. As a developer whose push is rejected, I want a clear git error naming the failing rule and commit, so that I can fix and retry.

### Force-push on merge request branches

47. As a merge request author, I want to force-push to my source branch while the merge request is Open or Approved (subject to branch protection on that source ref), so that I can rebase or fix history.
48. As a reviewer, I want approvals dismissed after force-push when the dismiss-on-update rule applies, so that I am not bound to rewritten commits I never saw.

### Merge execution

49. As a user with merge permission, I want to merge an **Approved** merge request through the platform, so that I do not need direct push access to the protected target.
50. As a merger, I want to choose **merge commit** (default), **squash merge**, or **fast-forward** when possible, so that teams control history shape.
51. As a protected-branch maintainer, I want to **lock** the allowed merge strategy per rule (e.g. squash-only on `main`), so that policy is enforceable.
52. As a merger, I want fast-forward to succeed only when linear; when FF is impossible, I want to be blocked with a clear message rather than silently falling back, so that I choose explicitly.
53. As a merger, I want an optional **delete source branch** checkbox (default off) at merge time, so that I can clean up after landing.
54. As a platform, I want server-side merge to reject execution if a dry-run merge reports conflicts, even if the UI previously showed mergeable, so that races are safe.
55. As a repository visitor, I want **rebase merge** deferred to a later release, so that v1 scope stays focused.

### Mergeability and conflicts

56. As a repository visitor, I want to see whether a merge request is **mergeable**, has **conflicts**, or is **checking**, so that I know if it can land.
57. As a merger on an Approved merge request with conflicts, I want the **Merge** action disabled until mergeable again, so that I cannot land broken merges.
58. As a merge request author, I want guidance to resolve conflicts **locally** (merge or rebase target into source, then push) in v1, so that expectations are clear without a web conflict editor.
59. As a repository visitor, I want to see when the source branch is **behind** the target, so that I know updates may be needed (with **Update branch** deferred to v2).

### Overview comments and review on diff

60. As a signed-in user with read access (and not blocked from participation where applicable), I want to post **overview comments** on the merge request, so that I can discuss the change holistically.
61. As a reviewer, I want to leave **line-level comments** on the unified diff in the **Changes** tab, so that feedback is tied to specific lines.
62. As a reviewer, I want one level of **replies** under each line comment root, so that threads stay structured like discussion sub-threads.
63. As a line comment root author or Writer+ member, I want to **resolve** a review thread with collapse semantics, so that addressed notes are visually quiet but preserved.
64. As a reviewer, I want line comments marked **Outdated** and collapsed when the diff changes, so that stale feedback does not look current.
65. As a comment author, I want to write in **Markdown** with the same safety posture as discussions, so that formatting is consistent and safe.
66. As a comment author, I want @mentions to notify mentioned users, so that I can draw attention.
67. As a reviewer, I want to use the standalone **Approve** button rather than bundled “submit review” flows in v1, so that approval is explicit and simple.

### Discussion linking

68. As a merge request visitor, I want to see **linked discussions** on the merge request page, so that context is one click away.
69. As a discussion visitor, I want to see **linked merge requests** when links exist, so that I can follow related code changes.
70. As a platform, I want merging a merge request with **`closes`** links to transition linked discussions to **Resolved**, so that fixed bugs close their threads automatically.
71. As a platform, I want `related` and `implements` links to remain informational in v1, so that linking is flexible without automatic lifecycle side effects.

### Default branch

72. As an Admin or Owner, I want to set and change the repository **default branch** in settings, so that merge targets and `@default` protection are explicit.
73. As a platform, I want the default branch initialized from existing git refs (`main`, then `master`, then first alphabetically) when unset, so that existing repositories migrate smoothly.
74. As an Admin or Owner, I want default branch changes validated against existing branch names, so that invalid configuration is rejected.
75. As a merge request author, I want open merge requests **not** retargeted when the default branch changes, so that in-flight work stays stable.

### Repository settings UI

76. As an Admin or Owner, I want a **Branches & push rules** section in repository settings, so that protection and push policy live alongside general settings.
77. As an Admin or Owner, I want to manage protected branch rules, allowlists, approval counts, force-push policy, and push rules from that section, so that policy is discoverable.
78. As an Admin or Owner, I want **Maintainer** labeled in the UI for the existing **Admin** repository role, so that terminology matches other forges without a new enum value.

### Notifications

79. As a merge request participant, I want to be auto-subscribed when I comment or approve, so that I follow threads I join.
80. As the merge request author, I want notification when someone **approves**, when **approvals are dismissed** after a push, when the merge request becomes **Approved**, and when it is **Merged** or **Closed**, so that I stay informed.
81. As a prior approver, I want notification when approvals are dismissed due to new commits, so that I know to re-review.
82. As a subscriber, I want in-app notifications with deep links to the merge request, so that the bell inbox stays unified.
83. As an email subscriber, I want immediate email with a stable subject prefix using merge request notation (e.g. `[owner/repo!7]`), so that mail clients group by proposal.
84. As a discussion subscriber, I want a **Resolved** notification when a **`closes`** link merge resolves my discussion, so that closure is visible in the existing inbox.

### Authorization and roles

85. As a platform, I want merge request read access to mirror repository read rules, so that visibility matches code browse.
86. As a platform, I want merge request write operations (create, comment, approve, merge, close) to require authenticated users with appropriate repository roles and branch permissions, so that policy is consistent.
87. As a blocked discussion participant, I want participation rules for merge requests aligned with repository blocked-user policy when that feature applies, so that moderation is coherent.

### API and integration

88. As a web client developer, I want merge request APIs documented in OpenAPI, so that the frontend stays in sync.
89. As a git client, I want pushes to protected refs rejected at the protocol layer with actionable errors, so that enforcement is real rather than advisory.
90. As an operator, I want protected-branch and push-rule evaluation centralized in access-check and storage validation, so that SSH and HTTPS git entry points behave identically.

## Implementation Decisions

### Architectural stance

- **MergeRequest** is a **separate aggregate** from **Discussion**. Reuse collaboration *infrastructure* (Markdown rendering, mention parsing, notification delivery, authorization patterns, frontend presentation components) but not discussion entities or routes.
- User-facing name: **Merge request** (nav label **Merge requests**). URL segment: `merge-requests`. Email/link notation: `!7` for merge request number vs `#42` for discussions.
- **Maintainer** in UI maps to existing repository role **Admin**; no new role enum in v1.
- **Same-repository branches only** in v1; cross-repo and fork-based merge requests are deferred.

### Major modules

#### 1. Repository Branch Policy (extends Repository feature)

**Interface:** read/update default branch; CRUD protected branch rules; CRUD push rule attachments per rule or repo scope.

**Responsibilities:**

- Persist `defaultBranchName` on repository (nullable until first ref sync or explicit Admin+ configuration).
- Protected branch rule fields: pattern (`main`, `release/*`, `@default`), block direct push (default true), allowed push roles (Owner, Admin, Writer multi-select), allowed push user IDs, require merge request to merge, required approval count, merge permission role threshold, force-push policy enum, dismiss approvals on new commits (default true), optional locked merge strategy, attached push rule set.
- Push rule types in v1: max file size, forbidden path globs, commit message regex (optional), require DCO Signed-off-by on each commit.
- Resolve `@default` pattern against stored default branch at evaluation time.
- Admin+ authorization for settings mutations.

Does **not** perform git operations or evaluate merge request state.

#### 2. Git Push Enforcement (extends access-check + storage receive-pack)

**Interface:** evaluate `(repository, user, ref, oldSha, newSha, pushPayloadMetadata)` → allow / deny with reason.

**Responsibilities:**

- On `WriteGit`, inspect target ref update; match against protected branch rules.
- Allow platform merge identity unconditionally for merge-result refs.
- Allow direct push when actor is on role or user allowlist for matching rule.
- Deny direct push to protected refs when not allowed; deny when push rules fail (file path, size, message, DCO).
- Detect force-push via old/new SHA relationship; apply force-push policy.
- Return errors suitable for git clients (HTTP/SSH receive-pack rejection messages).

Does **not** create merge requests or compute diffs.

#### 3. Storage Merge & Compare (extends storage internal API)

**Interface:** `GetDiff(baseSha, headSha)`, `CheckMergeability(targetSha, sourceSha)`, `ExecuteMerge(targetRef, sourceRef, strategy)`.

**Responsibilities:**

- Produce unified diff payload for web **Changes** tab (file list, hunks, line numbers for old/new sides).
- Dry-run mergeability via merge-tree or equivalent without mutating refs.
- Execute merge commit, squash merge, or fast-forward per strategy; fail on conflict.
- Expose replication/quorum behavior consistent with existing write routing (merge writes go through primary / quorum path).

Does **not** manage merge request lifecycle or approvals.

#### 4. Merge Request Core (new feature module)

**Interface:** create, read, list/filter, update metadata, transition status, track SHAs, allocate per-repo sequence number.

**Responsibilities:**

- Merge request aggregate: `repositoryId`, sequential `number`, `title`, optional `body`, `status`, `creatorUserId`, `sourceRef`, `targetRef`, `sourceHeadSha`, `targetBaseSha`, `isDraft`, timestamps.
- Status state machine:

```
[*] → Draft (when created as draft) or Open (when created ready)

Draft → Open     by author or Writer+
Open → Approved  automatically when all merge gates pass at current sourceHeadSha
Open → Closed    by author or Writer+
Approved → Merged by authorized merger via merge execution module
Approved → Closed by author or Writer+
Draft → Closed   by author or Writer+

No transition to Merged except from Approved
```

- Duplicate guard: at most one active (Draft/Open/Approved) MR per `(sourceRef, targetRef)` pair per repository.
- Creation validation: creator can push source; source ahead of target; source ≠ target.
- Update SHAs on branch movement (webhook-style polling or refresh on read — implementation choice: refresh on read + explicit refresh endpoint acceptable v1).
- Authorization mirrors repository read for visibility; mutations require appropriate roles and branch permissions.

Does **not** render Markdown, merge git refs, or send notifications directly.

#### 5. Merge Gates (new; v1 human approvals only)

**Interface:** `EvaluateGates(mergeRequest) → satisfied | pending[]`; register gate providers (extensibility for CI/CD).

**Responsibilities:**

- v1 gate provider: **RequiredApprovals** — count distinct approvals at current `sourceHeadSha` from eligible approvers (Writer+, not author).
- Approval records: `(mergeRequestId, userId, commitSha, createdAt)`.
- Dismiss all approvals when source HEAD changes if rule `dismissApprovalsOnPush` applies.
- When all gates satisfied, transition **Open → Approved**; when gates no longer satisfied after HEAD change, transition **Approved → Open** (recommended) or remain Approved with merge disabled — **decision: revert to Open** when approvals dismissed so Approved always means currently valid gates.

Does **not** execute merge or post review comments.

#### 6. Merge Execution Orchestrator

**Interface:** `Merge(mergeRequestId, strategy, deleteSourceBranch)`.

**Responsibilities:**

- Verify status is **Approved**, actor has merge permission, mergeability is clean at execution time.
- Call storage merge API with selected strategy (respect rule-locked strategy if configured).
- On success: set **Merged**, record merge commit SHA, process **`closes`** discussion links (resolve discussions), optional delete source branch ref.
- On conflict: fail with clear error; do not change MR status.

#### 7. Merge Request Comments (new)

**Interface:** overview comments CRUD; review comment CRUD with anchors; list threaded.

**Responsibilities:**

- **Overview comments:** merge-request-scoped, chronological, Markdown, edit, soft-delete (mirror discussion comment rules where applicable).
- **Review comments:** root + one reply level; anchor payload `(headCommitSha, filePath, lineNumber, diffSide)`; resolve/unresolve on roots (author or Writer+); outdated detection when diff at current head no longer contains anchor.
- Separate persistence from `discussion_comments` but parallel DTO fields for frontend adapter components.

Does **not** own diff generation.

#### 8. Discussion Links (extends Merge Request Core)

**Interface:** add/remove links `(mergeRequestId, discussionNumber, relationshipType)`.

**Responsibilities:**

- Relationship types: `closes`, `related`, `implements`.
- Parse `#n` references from description on create/update optional convenience.
- On **Merged**, for each `closes` link, invoke discussion resolve transition (Writer+ equivalent system action).

#### 9. Notifications (extends existing notification infrastructure)

**Interface:** emit merge-request event types; subscribe/unsubscribe.

**Responsibilities:**

- Extend notification entity with nullable `mergeRequestId`; add event types for approval, approval dismissed, approved, merged, closed, new comment, mention, review thread resolved.
- Auto-subscribe author, commenters, approvers; email subject `[owner/repo!n]`.
- Reuse bell UI with MR deep links.

#### 10. Web Client (merge request surfaces)

**Interface:** list, detail (Overview, Commits, Changes), create form, merge dialog, settings branches tab, post-push banner.

**Responsibilities:**

- Extract/shared components from discussions: Markdown editor, rendered Markdown/code blocks, comment thread + sub-thread resolve UI, code-attach modal, mention autocomplete, status badges.
- MR-specific shells: diff viewer (unified v1), approval widget, mergeability banner, linked discussions sidebar, merge strategy picker.
- Repository settings: default branch, protected rules editor, push rules editor, allowlist pickers (roles + members).
- Post-push banner on repository views when current branch is ahead of default.

### Schema (conceptual)

- `repositories.default_branch_name` — nullable string
- `protected_branch_rules` — repository_id, pattern, flags, allowed roles bitmask or join table, allowed user ids join, required_approval_count, merge_role_threshold, force_push_policy, dismiss_approvals_on_push, locked_merge_strategy nullable
- `push_rules` / `protected_branch_rule_push_rules` — rule type + JSON config
- `merge_requests` — core fields, status enum, refs, shas, number, is_draft
- `merge_request_approvals` — merge_request_id, user_id, commit_sha, created_at
- `merge_request_comments` — body, soft-delete, optional parent for replies, optional anchor columns, resolve columns on roots
- `merge_request_discussion_links` — merge_request_id, discussion_id, relationship_type
- `notifications` — add merge_request_id nullable; extend event type enum

### API surface (conceptual)

Repository settings:

- `GET/PATCH /repository/{id}/settings/default-branch`
- `GET/POST/PATCH/DELETE /repository/{id}/protected-branch-rules`
- Push rule configuration nested under rules or repo scope

Merge requests:

- `GET/POST /repository/by-slug/{owner}/{slug}/merge-requests`
- `GET/PATCH /repository/by-slug/{owner}/{slug}/merge-requests/{number}`
- `POST .../publish` (Draft→Open), `POST .../close`, `POST .../approve`, `POST .../merge`
- `GET/POST .../merge-requests/{number}/comments`
- `POST .../comments/{id}/resolve`, `POST .../unresolve`
- `GET .../merge-requests/{number}/changes` (diff payload)
- `GET .../merge-requests/{number}/mergeability`
- `GET/POST/DELETE .../merge-requests/{number}/discussion-links`

Exact route shapes follow existing API conventions and OpenAPI generation patterns.

### Merge gate extensibility (CI/CD forward compatibility)

Define merge gates as pluggable checks returning pass/fail + detail. v1 registers only **RequiredApprovals**. Future **PipelineStatus** gate consults latest pipeline for merge request head without renaming **Approved** — MR stays **Open** until pipeline gate passes, then joins human approvals to reach **Approved**.

### Assumptions

- Discussions feature (including sub-threads, notifications, Markdown) is available or ships in parallel; MR UI reuses its components.
- Git storage proxy, HTTPS/SSH dispatchers, and HA write quorum from existing PRDs remain the transport for push and merge writes.
- Platform merge uses a dedicated internal git identity recognized by push enforcement.
- Session cookie authentication for web UI; PAT on git only (consistent with discussions v1).
- Commits tab on merge request detail lists commits reachable from source since merge-base (standard forge semantics).
- **Approved → Open** on approval dismissal keeps **Approved** semantically equivalent to “all gates currently pass.”
- Comment pagination deferred; full thread load acceptable v1 unless performance requires otherwise.

### Suggested implementation order (tracer bullets)

| ID | Slice | Delivers | Blocked by |
|----|-------|----------|------------|
| MR-01 | Default branch persistence + settings field | Stored default, `@default` resolution | — |
| MR-02 | Protected branch + push rule CRUD | Policy API/DB | MR-01 |
| MR-03 | Git push enforcement | Real deny on protected refs + push rules | MR-02 |
| MR-04 | Storage compare / mergeability / merge execute | Diff + dry-run + merge | git storage proxy |
| MR-05 | Merge request core | Create, list, detail, lifecycle, dup guard | MR-01 |
| MR-06 | Approvals + merge gates | Approve, dismiss, Open↔Approved | MR-05 |
| MR-07 | Server-side merge + closes links | End-to-end land on target | MR-03, MR-04, MR-06 |
| MR-08 | Overview comments | Shared Markdown composer | MR-05 |
| MR-09 | Changes tab + review threads | Diff UI, anchors, outdated | MR-04, MR-08 |
| MR-10 | Notifications | In-app + email for MR events | MR-05, notification infra |
| MR-11 | Settings UI + post-push banner | Branches & push rules UX | MR-02, MR-05 |
| MR-12 | E2E integration tests | Protect → push → MR → approve → merge | MR-07, MR-09 |

**First demo milestone:** MR-07 — protect `main`, push feature branch, open MR, approve, merge (diff review may follow in MR-09).

Scaffold new merge-request backend feature via `agentGenCli new backend-feature merge-request --withDatabase --withApi`.

### Naming reference

| Concept | User-facing | Notes |
|---------|-------------|-------|
| Change proposal | Merge request | Not “pull request” in product UI |
| Admin role label | Maintainer | UI only; enum stays Admin |
| MR number in links | `!7` | vs discussion `#42` |
| Target alias | `@default` | Protected rule pattern |
| Review thread | Review thread | Same UX patterns as discussion sub-thread |

## Testing Decisions

### Principles

- Test **observable behavior** through HTTP APIs, git push outcomes, and authorization status codes — not internal state machine private methods.
- Prefer table-driven tests for role matrices (Reader, Writer, Admin, Owner, anonymous, allowlisted user, platform identity).
- Git integration tests use existing Docker Compose / HTTPS git scripts patterns (`e2e-https-git-test.sh`, access-check controller tests).
- UI component reuse is validated by API DTO shape tests supplying fields the shared thread components require (`replyCount`, `isResolved`, `isOutdated`).

### Modules and prior art

| Module | Test focus | Prior art |
|--------|------------|-----------|
| Repository Branch Policy | Default branch validation; `@default` pattern resolution; rule CRUD auth Admin+ | Repository CRUD handler tests, settings controller tests |
| Git Push Enforcement | Direct push denied on protected ref; allowlist bypass; push rule failures; force-push policy | `RepositoryAccessChecksControllerTests`, git integration scripts |
| Storage Merge & Compare | Mergeability reflects conflict; merge strategies produce expected refs; FF blocked when not linear | Storage content API tests, HA quorum push tests |
| Merge Request Core | Lifecycle transitions; duplicate pair guard; draft/open publish; SHA refresh | Discussion core handler tests (sequential numbering, status) |
| Merge Gates | Approval eligibility; dismiss on push; Open↔Approved; author cannot approve | Discussion lifecycle approval-style patterns |
| Merge Execution | Merged only from Approved; closes links resolve discussions; merge blocked on conflict | E2E compose scripts |
| MR Comments | Reply depth limit; resolve permissions; outdated flag when head changes | Discussion sub-thread handler tests |
| Discussion Links | closes on merge; related unchanged | Discussion resolve handler tests |
| Notifications | Subject `[owner/repo!n]`; approval/dismiss events | Discussion notification tests |

### Integration scenarios

- Unprotected repo: direct push still works; MR optional.
- Protect `main`: Writer direct push denied; feature branch push allowed; MR merge lands on `main`.
- Allowlisted Admin direct push to protected ref succeeds.
- Push with forbidden path / missing DCO rejected with named error.
- Force-push to protected target denied; force-push to MR source dismisses approvals.
- Create MR Draft → publish Open → two approvals → Approved → merge squash → Merged.
- MR with `closes #12` resolves discussion 12 on merge.
- Approved MR with target advance causing conflict: merge disabled until fixed.
- Public repo anonymous read MR; unauthenticated create returns 401.
- Private repo anonymous 404; outsider 403.

### Out of scope for automated tests in v1

- Visual regression of merge request UI and diff rendering.
- Email client threading behavior for `!n` subjects.
- Complex diff anchor relocation across large renames (smoke tests only).
- Performance of full diff on very large changesets.

## Out of Scope

- **Forks** and **cross-repository** merge requests.
- **Auto-create merge request** on every push (banner only in v1).
- **Rebase merge** strategy.
- **Web-based conflict resolution** editor.
- **Update branch** / rebase-without-checkout button (v2).
- **Required reviewers** shortlist beyond approval count (named reviewer lists).
- **CODEOWNERS** automatic review assignment.
- **GPG signed commit** requirement.
- **Secret scanning** push rule (defer to CI/CD).
- **Require linear history** push rule.
- **Merge request templates** and **default description** boilerplate.
- **Labels**, milestones, assignees on merge requests (can add later; discussions keep assignees).
- **Bundled review submission** (GitHub-style “Submit review” with approve/request-changes in one action).
- **Side-by-side diff** (unified only v1).
- **Real-time** diff/comment updates (WebSockets); refresh/polling acceptable.
- **PAT authentication** on merge request web APIs (session auth v1).
- **CI/CD pipeline** execution and pipeline gate UI (separate PRD; only extensibility hook in this PRD).
- **Audit UI** for all merge actions beyond git history and MR event timestamps.
- **Email digest** notifications.

## Further Notes

### Relationship to repository discussions

Discussions remain the general collaboration layer. Merge requests handle code landing. Link types express traceability; **`closes`** is the only automatic lifecycle bridge in v1. Review comments are not mirrored into discussions automatically. Shared frontend components should live in a neutral shared layer (e.g. `components/collaboration/`) to avoid discussion feature importing merge-request feature or vice versa.

### Relationship to CI/CD (future PRD)

The **Approved** state is the contract surface for pipeline gates. Implement merge gates as a small registry interface in MR-06 so the pipelines feature adds a gate provider without changing merge request statuses. Pipelines may also trigger on **Approved** entry (deploy previews) — exact triggers belong in the CI/CD PRD.

### Relationship to existing git transport

Push enforcement must behave identically for SSH (when enabled) and HTTPS Smart HTTP through dispatcher access-checks. Server-side merge executes as the platform identity through the same storage write path as quorum-aware pushes.

### Update to repository discussions PRD deferred item

The repository discussions PRD listed “change proposals / merge requests linked to discussions” as out of scope. This PRD supersedes that deferral for linking and **`closes`** behavior. Discussion entities remain unchanged except for system-initiated resolve on linked merge.
