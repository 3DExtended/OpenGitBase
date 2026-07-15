<!-- forge: #60 -->

# Commit page navigation and error parity

## Metadata

- ID: fix-01
- Type: AFK
- Status: ready
- Source: code review (Jul 2026)

## What to build

Bring the commit change view page to parity with repository browse pages for navigation, error handling, and replication lag — fixing stale-data bugs when route params change.

**Behavior:**

- Reload commit data when `owner`, `repo`, or `sha` route params change (not only `sha`).
- Distinguish HTTP 403 (forbidden), 503 (unavailable), and 404 (not found) with the same user-facing messages used on tree/blob browse.
- Show `RepoSyncBanner` when the API reports replication lag (parse `replicationLag` on `RepositoryCommit` client type).
- Guard against stale responses when params change rapidly (abort controller or request sequence id).
- Root commit with empty `rootFiles` shows an explicit empty state.
- Copy-SHA handles clipboard permission failures with user feedback instead of throwing.

## Acceptance criteria

- [ ] Navigating between two repos with the same SHA loads the correct commit for the new repo
- [ ] Private repo commit as anonymous outsider shows forbidden/unavailable messaging consistent with browse pages
- [ ] Replication lag banner appears when API returns lag indicator
- [ ] Rapid SHA changes do not display data from a superseded request
- [ ] Empty root file list shows a labeled empty state
- [ ] Clipboard failure shows toast or inline error
- [ ] Vitest and/or Playwright tests cover cross-repo navigation and at least one non-404 error path

## Blocked by

- None — can start immediately

## Findings covered

- High: stale commit data when owner/repo changes
- Medium: replication lag not surfaced on commit page
- Medium: commit page lacks 403/503 handling
- Low: root commit empty file list has no empty state
- Low: copySha clipboard error handling
- Low: no in-flight request guard

## Notes

Storage outage mapped to API 404 (vs 503) is an API-layer concern; this slice should handle whatever status codes the API returns today and can add a follow-up API fix separately if needed.
