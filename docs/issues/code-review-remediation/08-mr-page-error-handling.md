<!-- forge: #61 -->

# MR page error handling and review thread correctness

## Metadata

- ID: fix-02
- Type: AFK
- Status: ready
- Source: code review (Jul 2026)

## What to build

Fix merge request detail page regressions: silent API failures, incorrect review reply anchoring, and unauthenticated mutation UI.

**Behavior:**

- `loadAll()` surfaces errors from parallel fetches (comments, changes, commits, discussion links) — partial failure shows an alert or inline error, not empty sections that look like "no data".
- Review thread replies pass the actual `diffSide` from the anchor or line selection (`old` vs `new`), not a hardcoded `new`.
- `resolveComment`, `unresolveComment`, and `removeDiscussionLink` check `result.error` and surface via `replyError` (or equivalent) like other mutations.
- Linked-discussion add/remove controls visible only when `auth.isAuthenticated`.

## Acceptance criteria

- [ ] Simulated failure on discussion-links fetch shows error UI, not empty sidebar
- [ ] Reply on a removed (`old`) line attaches with `diffSide: 'old'` and appears on correct line
- [ ] Failed resolve/unresolve/link-remove shows user-visible error
- [ ] Unauthenticated MR view hides link/remove discussion controls
- [ ] Existing Playwright MR visual/regression tests pass; add or extend test for diffSide reply if feasible with mocked API

## Blocked by

- None — can start immediately

## Findings covered

- Medium: MR detail page silently ignores parallel API failures
- Medium: MR review replies hardcode `diffSide: 'new'`
- Medium: resolve/unresolve/delete actions ignore API errors
- Low: linked-discussion UI has no auth guard

## Notes

`MergeRequestLinkedDiscussions` component auto-import was fixed separately; this slice addresses runtime behavior on the MR detail page.
