# Anchored code comments

## Metadata

- ID: disc-09
- Type: AFK
- Status: ready
- Source: docs/prd/repository-discussions.md

## Parent

[PRD: Repository Discussions (Threads, Code Comments, Notifications)](../../prd/repository-discussions.md)

## What to build

**Anchored (code) comments** tied to source lines, integrated with repository blob browsing.

**Anchor payload** (on comment or sidecar row):
- `ref` — ref being browsed at comment time
- `commitSha` — resolved SHA at comment time
- `filePath`
- `line` (start), optional `endLine`

**Create flow:**
- From blob page: select line → open create/add comment flow.
- New discussion: auto-suggest title `Note on \`{path}:{line}\``; user may edit.
- Existing discussion: add anchored comment to thread.

**Anchor resolver service:**
- Input: repository, current ref, stored anchor.
- Output: `Located` (path + line on current tip), `Outdated` (moved/edited), or `Orphaned` (file/hunk gone).
- Use git history via storage content APIs; no custom object DB.

**UI:**
- Anchored comments show code context snippet and jump link when located.
- Outdated/orphaned: clear badge; discussion and comment text remain visible.

Anchored comments share comment permissions, Markdown, soft-delete, and lifecycle hooks from disc-04.

## Acceptance criteria

- [ ] Create anchored comment stores ref, commitSha, path, line
- [ ] Default ref is the ref currently shown on blob page
- [ ] New discussion from anchor requires title; auto-suggest pre-fills
- [ ] Anchored comment appears in discussion thread with code context
- [ ] Resolver returns located line when file unchanged on branch tip
- [ ] Resolver shows outdated when line moved within file (best-effort)
- [ ] Orphaned anchor when file deleted; discussion still intact
- [ ] Anchored comment participates in Engaged/reopen rules same as thread comment
- [ ] Blob page UI: line selection triggers comment/discussion flow
- [ ] Tests: anchor persistence; resolver smoke with fixture repo

## Blocked by

- [04-thread-comments-engagement-lifecycle.md](./04-thread-comments-engagement-lifecycle.md)
- [06-blob-view-text-download-size-cap.md](../repository-web-browsing/06-blob-view-text-download-size-cap.md)

## User stories covered

- 9, 33, 34, 35, 36, 37, 38, 64

## Notes

- Complex merge/rename scenarios: best-effort in v1; document limitations in UI copy.
- Does not require repo-browse-07 (image/markdown toggle); text blob view sufficient.
