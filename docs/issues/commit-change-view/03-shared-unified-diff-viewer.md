# Shared unified diff viewer

## Metadata

- ID: cv-03
- Type: AFK
- Status: ready
- Source: docs/prd/commit-change-view.md

## Parent

[PRD: Commit Change View (Clickable Commits & Per-Commit Diff)](../../prd/commit-change-view.md)

## What to build

Extract merge request **Changes** tab diff rendering into a shared Vue component usable in read-only and interactive modes.

**Component (conceptual name: `RepoUnifiedDiff`):**

- Renders file cards with path, change type badge, unified hunks, and syntax-highlighted lines (same presentation as today’s MR diff).
- **`readOnly` mode** — no line click handlers, no review thread slots, no “add comment” affordances.
- **Interactive mode (default for MR)** — preserves existing line selection, review thread rendering under hunks, resolve/outdated badges, and outdated thread collapse behavior.

Refactor the merge request detail **Changes** tab to use the shared component without regressing review-comment functionality.

## Acceptance criteria

- [ ] MR **Changes** tab behavior unchanged: line comments, threads, resolve, outdated badges still work
- [ ] Shared component accepts diff file payload matching merge request **Changes** DTO shape
- [ ] `readOnly: true` hides all review-interaction UI
- [ ] Binary files summarized without inline hunks (same as current MR behavior)
- [ ] Playwright visual regression: MR Changes tab snapshot matches pre-refactor baseline (or intentional approved update)

## Blocked by

- None — can start immediately (MR Changes tab already shipped in [mr-11](../merge-requests/11-changes-tab-diff-and-review-threads.md))

## User stories covered

- 16, 17, 33, 34

## Notes

- Can proceed in parallel with **cv-02**.
- Commit page consumes this component in **cv-04** with `readOnly: true`.
- Side-by-side diff remains out of scope.
