<!-- forge: #66 -->

# Commit page shell

## Metadata

- ID: cv-04
- Type: AFK
- Status: ready
- Source: docs/prd/commit-change-view.md

## Parent

[PRD: Commit Change View (Clickable Commits & Per-Commit Diff)](../../prd/commit-change-view.md)

## What to build

Nuxt commit change view page at `/{owner}/{repo}/commit/{sha}` wired to the commit read API and shared diff viewer.

**Page behavior:**

- Load commit via `GET .../commits/{sha}` on mount.
- Replace browser URL with canonical full SHA after successful load when input was abbreviated.
- **Header:** commit message, short SHA, author name, relative authored time, parent SHA list (plain links for now — `RepoCommitLink` lands in cv-05), diff stats badge (+/−/files).
- **Actions:** **Browse files** → tree browse at commit SHA; **Copy SHA** copies full hash to clipboard.
- **Body:** `RepoUnifiedDiff` with `readOnly: true` when `kind: "diff"`; file list with blob browse links when `kind: "root"`.
- **Navigation:** repo breadcrumb (`owner/repo` → home); labeled back link when `from` query present (parse `mr/{n}`, `discussions/{n}` patterns).
- Show replication lag banner when API indicates lag (consistent with browse pages).
- Empty diff state for metadata-only commits (clear message, not an error page).

**Browse files prerequisite:** verify tree/blob routes accept detached commit SHAs as `{ref}`; extend content ref resolution minimally if only branch/tag names work today.

## Acceptance criteria

- [ ] Navigating to `/{owner}/{repo}/commit/{fullSha}` renders message, author, date, stats, and diff hunks for a linear commit
- [ ] Abbreviated SHA in URL resolves and URL bar updates to full SHA
- [ ] Root commit renders file list; each path links to blob browse at that commit SHA
- [ ] Parent SHAs displayed and link to sibling commit pages
- [ ] Copy SHA writes full 40-character hash to clipboard
- [ ] Browse files opens tree at commit SHA
- [ ] `?from=mr/7` shows back link to merge request detail; unknown/missing `from` shows repo breadcrumb only
- [ ] 404/error states for unknown commit and unauthorized access match browse UX
- [ ] Playwright visual regression: commit page with diff snapshot
- [ ] Web API client method for commit read endpoint

## Blocked by

- [02-commit-read-api.md](./02-commit-read-api.md)
- [03-shared-unified-diff-viewer.md](./03-shared-unified-diff-viewer.md)

## User stories covered

- 1–4, 9–21, 14–15, 18–20, 38, 40

## Notes

- Parent links can use plain `NuxtLink` until **cv-05** introduces `RepoCommitLink`; migrate parent links when cv-05 lands.
- i18n keys for new strings (stats label, copy SHA, browse files, back labels, empty diff).
- Prior art: merge request detail page layout, repository browse pages for lag banner and error cards.
