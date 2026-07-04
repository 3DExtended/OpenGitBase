# PRD: Commit Change View (Clickable Commits & Per-Commit Diff)

## Problem Statement

OpenGitBase shows commits in several places — merge request **Commits** tabs, review-comment anchors, discussion anchors, and branch/tag metadata — but commit SHAs are not actionable. Users cannot open a dedicated view of what a single commit changed. The merge request **Changes** tab shows a cumulative diff (merge-base through source HEAD), not per-commit patches. There is no repository-scoped commit page, no API to fetch a single commit's diff, and no shared navigation pattern for commit links across the web UI.

Developers expect forge-style commit browsing: click a commit anywhere in the product, land on a page showing that commit's message, author, parent linkage, change statistics, and a unified diff against its first parent (or, for root commits, a listing of all files introduced). Reviewers who follow a link from a merge request or anchored comment should be able to return to their prior context without relying on browser history alone.

## Solution

Add a **commit change view**: a repository-scoped page at `/{owner}/{repo}/commit/{sha}` backed by a new read API that returns commit metadata, diff statistics, and either a unified diff (parent → commit) or a root-commit file tree. Introduce shared frontend modules — a **`RepoCommitLink`** component and a read-only **`RepoUnifiedDiff`** viewer extracted from the merge request **Changes** tab — and wire every commit SHA surfaced in the repository UI through those modules.

SHA URLs accept full or uniquely resolvable prefixes; after load the canonical URL normalizes to the full 40-character hash. Authorization matches repository web browsing (public anonymous read; private 404/403 semantics consistent with tree and blob endpoints). Optional `from` query parameters on links preserve context-aware back navigation (e.g. back to merge request `!7` or discussion `#3`).

## User Stories

### Discovery and access

1. As a repository visitor with read access, I want to open `/{owner}/{repo}/commit/{sha}` for any commit reachable in that repository, so that I can inspect individual changes without cloning.
2. As a repository visitor, I want commit URLs to accept abbreviated SHAs when unambiguous, so that links match the short forms already shown in lists.
3. As a repository visitor, I want ambiguous or unknown SHAs to return **404**, so that mistaken links fail clearly.
4. As a repository visitor, I want the page to normalize the browser URL to the full commit SHA after resolution, so that bookmarks and shared links are stable.
5. As an anonymous visitor on a **public** repository, I want to view commit pages without signing in, so that open-source history is inspectable in the browser.
6. As an anonymous visitor on a **private** repository, I want commit pages to return **404**, so that private repositories are not revealed.
7. As a signed-in user without read access to a private repository, I want commit pages to return **403**, so that permission semantics match tree and blob browsing.
8. As a repository visitor, I want replication lag behavior on commit reads to match other content endpoints (serve replica data; show syncing banner when applicable), so that browsing stays off the primary storage node.

### Commit page content

9. As a repository visitor, I want to see the commit subject and body message on the commit page, so that intent is clear.
10. As a repository visitor, I want to see the author name and authored timestamp, so that I know who made the change and when.
11. As a repository visitor, I want to see diff statistics (files changed, insertions, deletions), so that I can gauge scope at a glance.
12. As a repository visitor, I want parent commit SHAs displayed as clickable links, so that I can walk history backward (including merge commits with multiple parents).
13. As a repository visitor viewing a normal commit, I want a unified diff of the commit against its **first parent** (`git show` semantics), so that I see only the changes introduced by that commit.
14. As a repository visitor viewing a **root commit** (no parent), I want a listing of all file paths in the repository at that commit, so that I still have a meaningful "change view" for the initial commit.
15. As a repository visitor on a root commit, I want each listed file path to link to blob browse at that commit SHA, so that I can open file contents from the listing.
16. As a repository visitor, I want binary files in the diff summarized appropriately (no inline hunks), consistent with merge request diff presentation, so that behavior matches expectations from the **Changes** tab.
17. As a repository visitor, I want syntax-highlighted diff hunks using the same rendering as merge request diffs, so that the reading experience is consistent.
18. As a repository visitor, I want a **Browse files** action linking to tree browse at this commit SHA, so that I can explore the full tree at that revision.
19. As a repository visitor, I want a **Copy SHA** control for the full commit hash, so that I can paste it into git commands or other tools.
20. As a repository visitor, I want a repository breadcrumb (`owner/repo`) on the commit page, so that I can navigate back to the repository home.
21. As a repository visitor arriving from a merge request or discussion, I want a labeled back link when the referring context is known, so that I return to review or conversation without guessing.

### Merge request integration

22. As a merge request reviewer, I want each commit on the **Commits** tab to be a single clickable target (whole card), so that opening a commit's changes is obvious and works on mobile.
23. As a merge request reviewer, I want commit links from the **Commits** tab to include merge-request context for back navigation, so that I return to `!n` after inspecting a commit.
24. As a merge request reviewer, I want review-comment anchors to show the anchored commit SHA as a clickable link alongside `filePath:line`, so that I can jump to the revision the comment refers to.
25. As a merge request reviewer, I want the anchor header format to read as `path:line` followed immediately by the short SHA (no separator dot), so that the layout stays compact.
26. As a merge request reviewer, I want the commit page diff for a listed commit to match what that commit introduced (first-parent patch), which may differ from the cumulative **Changes** tab diff, so that per-commit inspection is accurate.

### Discussion integration

27. As a discussion participant, I want anchored comment previews to show the stored `commitSha` as a clickable link next to `filePath:line`, so that I can inspect the revision at comment time.
28. As a discussion participant, I want discussion context preserved in the `from` query when opening a commit from an anchor, so that back navigation returns to the discussion thread.

### Ref picker and branch metadata

29. As a repository visitor, I want branch and tag rows in the ref picker to show the tip commit SHA as a separate clickable chip, so that I can inspect the tip commit without leaving browse context.
30. As a repository visitor, I want selecting a branch or tag name in the ref picker to continue opening tree browse (unchanged default), so that existing navigation is not broken.
31. As a repository visitor, I want the SHA chip beside a ref to open the commit change view for that ref's `commitSha`, so that ref tips are first-class entry points to commit history.

### Shared linking infrastructure

32. As a frontend maintainer, I want a single **`RepoCommitLink`** component used everywhere a commit SHA is linked, so that URL rules, styling, and `from` context stay consistent.
33. As a frontend maintainer, I want a shared read-only unified diff viewer reused by the commit page and merge request **Changes** tab, so that diff rendering does not diverge.
34. As a merge request reviewer, I want the **Changes** tab to retain line-level review affordances (comment threads, resolve, outdated badges), which the commit page omits, so that review workflows stay on merge requests only.

### Edge cases

35. As a repository visitor, I want merge commits to diff against the first parent only on the commit page, so that the view matches standard `git show` behavior rather than combining all parents.
36. As a repository visitor opening a commit with multiple parents, I want each parent SHA listed and clickable, so that I can explore alternate parent lines manually (parent picker deferred).
37. As a platform, I want commit resolution to use git object verification on the storage node, so that only commits present in the repository are served.
38. As a repository visitor, I want empty diffs (e.g. merge commits with no tree change relative to first parent) to render a clear empty state rather than erroring, so that metadata-only commits remain viewable.

### Operator and consistency

39. As a security reviewer, I want commit diff responses for private repositories marked **no-store**, consistent with other private content responses, so that caches do not retain unauthorized data.
40. As a developer, I want the commit page and API to work in the default Docker Compose stack, so that local development and E2E tests can exercise the full flow.

## Implementation Decisions

### Architectural overview

The feature spans three layers already present in OpenGitBase: **storage** (bare-repo git commands), **API** (repository-scoped read endpoints with access checks and replica selection), and **web** (Nuxt pages and shared Vue components). No new database entities are required; commits are derived entirely from git objects.

### Deep modules (testable interfaces)

| Module | Responsibility | Interface (conceptual) |
|--------|----------------|------------------------|
| **Commit resolution** | Resolve full or prefix SHA to a verified commit object; reject ambiguous or missing SHAs | `ResolveCommit(repo, shaPrefix) → CommitMeta \| NotFound \| Ambiguous` |
| **Commit parent & stats** | Read parents, author, message, timestamps; compute numstat summary | `GetCommitMetadata(gitDir, sha) → CommitHeader` |
| **Commit patch** | Produce unified diff vs first parent, or root file tree when parentless | `GetCommitChanges(gitDir, sha) → DiffPayload \| RootTreePayload` |
| **Commit read API** | Orchestrate access check, replica routing, caching; single JSON response | `GET .../commits/{sha}` |
| **RepoCommitLink** | Build commit URLs, optional `from` query, display short or full SHA | Vue component + path helper |
| **RepoUnifiedDiff** | Render file cards, hunks, highlighted lines; optional read-only mode | Vue component; props: files, readOnly |
| **Commit page shell** | Header, stats, parents, actions, diff or root tree, back link | Nuxt page |

These modules are intentionally narrow: storage helpers know git, the API knows authorization and DTO mapping, shared UI knows presentation — matching the existing split used by repository content browsing and merge request compare.

### Storage layer

Extend the internal storage HTTP surface (alongside existing diff and commit-list helpers used by merge requests) with commit-specific operations:

- **Resolve SHA** — `git rev-parse` (or equivalent) with abbreviation rules; distinguish not found vs ambiguous.
- **Commit metadata** — `git log -1` (or cat-file) for message, author, authored date, parent SHAs.
- **First-parent diff** — reuse the existing unified diff parser with `parent^..commit` or `git show --format= --patch` against first parent; when no parent exists, skip diff and invoke recursive tree listing (`git ls-tree -r`) for root commits.
- **Diff statistics** — derive from existing `--numstat` parsing already used for merge-range diffs: files changed, insertion count, deletion count.

Root commits return a **RootTreePayload** (flat or nested path list with blob mode metadata) instead of hunks. Normal commits return a **DiffPayload** structurally compatible with merge request **Changes** file/hunk/line DTOs so the shared diff viewer consumes one shape.

### API layer

Add a repository-scoped read endpoint on the same controller family as tree/blob/refs:

```
GET /api/repository/by-slug/{owner}/{slug}/commits/{sha}
```

**Response (conceptual shape):**

```
{
  sha, shortSha,
  message, authorName, authoredAt,
  parents: [{ sha, shortSha }],
  stats: { filesChanged, insertions, deletions } | null,
  kind: "diff" | "root",
  files: [...]   // diff files OR root tree entries with browse paths
}
```

**Behavior:**

- Authorization identical to `GET .../content/tree` and `GET .../content/blob` (reuse repository read-access service).
- Replica selection and Redis caching follow repository web browsing patterns; private responses include `Cache-Control: no-store`.
- Prefix SHA resolution happens before diff work; canonical full `sha` always returned in the body.
- **404** for unknown/ambiguous SHA or missing repository; **403**/**404** for unauthorized private access per existing conventions.

No merge-request-scoped commit endpoint — the commit page is repository-global so links work from discussions, ref tips, and merge requests alike.

### Web layer

**New route:** `/{owner}/{repo}/commit/{sha}`

**Page behavior:**

- Fetch single commit API on load; replace URL with full SHA via router replace when response canonicalizes hash.
- Header: message, short SHA, author, relative time, parent links (`RepoCommitLink`), stats badge.
- Actions: **Browse files** → `/{owner}/{repo}/tree/{fullSha}`; **Copy SHA** button.
- Body: `RepoUnifiedDiff` in read-only mode for `kind: "diff"`; file list with blob links for `kind: "root"`.
- Back navigation: if `from` query present (e.g. `mr/7`, `discussions/3`), render labeled back link; else breadcrumb to repo home only.

**`RepoCommitLink` component:**

- Props: `owner`, `repo`, `sha`, optional `display` (`short` default), optional `from` context string.
- Renders monospace link styling consistent with existing forge accents.
- Used on: MR commit cards, MR/discussion anchor headers, ref picker SHA chips, commit page parent links.

**`RepoUnifiedDiff` extraction:**

- Refactor merge request **Changes** tab diff rendering into shared component.
- Merge request page passes review-thread slots and line-click handlers; commit page passes `readOnly: true` (no review comment UI).

**Anchor header format (confirmed):** `filePath:line` immediately followed by short SHA link — **no dot** between line number and SHA.

**Ref picker change:** Keep `<select>` branch/tag name → tree browse. Add visible SHA chip (`RepoCommitLink`) beside picker showing current ref's `commitSha` (and optionally in dropdown labels as secondary text if space allows).

**MR Commits tab:** Entire card wrapped in link (or card with click handler) to commit page with `from=mr/{number}`.

### Assumptions

- Detached SHA refs work in existing tree/blob routes (or will be extended minimally if today only branch/tag names are accepted — verify during implementation; PRD assumes `tree/{sha}` browse at commit is supported or added as part of **Browse files** action).
- Merge request diff DTO mapping remains the canonical diff file shape; commit diff reuses it rather than introducing a parallel schema.
- Side-by-side diff remains out of scope (unified only), consistent with merge requests v1.
- Parent-picker UI for multi-parent merge commits is out of scope; all parents listed, diff vs first parent only.
- GPG signatures, co-author trailers, and avatar enrichment are out of scope for v1 header.
- Commit pages do not support line-level comments (review stays on MR **Changes** tab).

### Suggested implementation order (tracer bullets)

| ID | Slice | Delivers | Blocked by |
|----|-------|----------|------------|
| CV-01 | Storage commit helpers | Resolve SHA, metadata, first-parent diff, root tree | git storage content APIs |
| CV-02 | Commit read API | `GET .../commits/{sha}` with auth + DTO | CV-01 |
| CV-03 | RepoUnifiedDiff extraction | Shared diff viewer; MR Changes tab migrated | MR Changes tab exists |
| CV-04 | Commit page shell | Route, header, stats, actions, diff/root render | CV-02, CV-03 |
| CV-05 | RepoCommitLink + MR Commits tab | Clickable commit cards with back context | CV-04 |
| CV-06 | Anchors + ref picker SHAs | Discussion/MR anchor SHA links; ref chip | CV-05 |
| CV-07 | Tests | API, visual, E2E click-through | CV-06 |

**First demo milestone:** CV-05 — open merge request **Commits** tab, click a commit, land on commit page with correct first-parent diff and back link to `!n`.

## Testing Decisions

### Principles

- Test **observable behavior** through HTTP status codes, JSON payload shape, rendered page content, and navigation outcomes — not internal git command strings or private mapper methods.
- Prefer table-driven tests for authorization (anonymous public, anonymous private, outsider authenticated, member) mirroring repository browse tests.
- Storage helpers get focused unit/integration tests against fixture bare repos (root commit, linear history, merge commit, ambiguous prefix).
- UI tests validate user-visible links and diffs; avoid asserting component prop wiring unless it is the only stable seam.

### Modules and prior art

| Module | Test focus | Prior art |
|--------|------------|-----------|
| Storage commit helpers | SHA resolve; first-parent diff hunks; root tree listing; ambiguous prefix error | Storage merge/compare tests (`get_diff`, `list_commits`), storage content API tests |
| Commit read API | 200 payload shape; stats; parent list; 404 unknown/ambiguous; auth matrix | `RepositoryContentController` browse tests, merge request commits endpoint tests |
| RepoUnifiedDiff | Renders file headers and hunk lines from fixture payload; read-only hides review UI | Merge request visual spec (`merge-requests.spec.ts`) |
| Commit page | Header fields; copy SHA; browse files link; canonical URL replace | Browse E2E patterns (`BrowseE2eTests`) |
| RepoCommitLink | Correct href including `from` query | Discussion anchor link tests (if any) |
| MR Commits tab click-through | Click card → commit page URL; diff content visible; back link returns to MR | `MergeRequestE2eTests`, Playwright visual MR spec |

### Required test layers (explicit user requirement)

1. **API integration tests** — full endpoint coverage: linear commit diff, root commit tree, merge commit (first-parent empty diff allowed), prefix resolution, ambiguous prefix 404, private/public auth.
2. **Playwright visual regression** — commit page with diff snapshot; MR **Commits** tab with clickable cards snapshot (animations disabled per existing visual test helpers).
3. **E2E click-through** — compose stack: seed repo with multiple commits; open merge request; navigate **Commits** tab; click commit card; assert commit page heading/message and at least one expected diff hunk or root file entry; use back link or `from` context to return to merge request detail.

Optional follow-up: discussion anchor SHA click E2E once discussion visual fixtures include anchored commits.

### Integration scenarios

- Public repo: anonymous `GET .../commits/{sha}` returns 200; commit page renders without auth.
- Private repo: anonymous 404; outsider 403; member 200.
- Root commit: API `kind: "root"` with file paths; page lists paths linking to blob browse.
- Two-commit branch: second commit diff shows only second commit's line changes vs first parent.
- Abbreviated SHA in URL resolves when unique; canonical full SHA appears after navigation.
- MR **Commits** tab E2E: card click lands on commit page with matching short SHA in header.
- Ref picker SHA chip navigates to commit page without changing selected ref browse target.

### Out of scope for automated tests in v1

- Performance of very large root-commit tree listings or megabyte diffs.
- Visual pixel-perfect diff syntax theme variants across browsers beyond existing Playwright matrix.
- Email or notification flows (commits do not emit events in v1).

## Out of Scope

- **Commit history page** / full log browser for a branch (only per-commit view; no `/{owner}/{repo}/commits` timeline in v1).
- **Compare two arbitrary commits** outside merge request compare (existing MR **Changes** tab remains the range compare surface).
- **Side-by-side diff** on commit page.
- **Parent picker** for merge commits (diff always vs first parent; other parents link-only).
- **Line-level comments** on commit page (review comments remain merge-request scoped).
- **Blame**, **cherry-pick**, **revert**, and other git actions from the web UI.
- **Cross-repository** or fork commit links.
- **GPG / signed commit** verification UI.
- **Email notifications** for commit page views.
- **RSS/Atom** commit feeds.

## Further Notes

### Relationship to merge requests

Merge request **Changes** shows cumulative diff from merge-base to current source HEAD. Commit change view shows **per-commit** first-parent patches. Reviewers may see different content on the two surfaces for the same SHA; both are correct for their scope. Documentation and UI copy should not imply the commit page equals "this MR's changes."

### Relationship to repository browsing

Tree and blob browse remain ref-name-first (`main`, tags). Commit page complements browse by answering "what did this revision change?" **Browse files** bridges commit view back into tree navigation at the detached SHA.

### Naming reference

| Concept | User-facing | Notes |
|---------|-------------|-------|
| Per-commit diff page | Commit | Route segment `/commit/{sha}` |
| Cumulative MR diff | Changes | Tab on merge request detail |
| Short SHA display | 8 characters | Full SHA in copy action and canonical URL |
| Anchor header | `path:line` + SHA | No dot separator between line and SHA |
| Back context query | `from=mr/7`, `from=discussions/3` | Set by `RepoCommitLink` at source |

### Open question for implementation (non-blocking)

Verify whether tree/blob routes already accept raw commit SHAs as `{ref}`; if not, extend ref resolution in the content API to treat 40-hex refs as detached commits when implementing **Browse files**. This is an implementation detail discovered during CV-04, not a product scope change.
