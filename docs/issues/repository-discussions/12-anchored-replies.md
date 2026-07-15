<!-- forge: #197 -->

# Anchored replies

## Metadata

- ID: disc-12
- Type: AFK
- Status: ready
- Source: docs/prd/discussion-sub-threads.md

## Parent

[PRD: Discussion Sub-Threads (Replies and Per-Comment Resolution)](../../prd/discussion-sub-threads.md)

## What to build

**Git-anchored replies** on sub-threads: optional anchor on reply create, same payload and resolver behavior as top-level anchored comments.

**Reply create with anchor:**
- Optional anchor payload on `POST` comment when `parentCommentId` is set: `ref`, `commitSha`, `filePath`, `line`, optional `endLine`.
- Anchor may point at different file/line than the root comment's anchor.
- Reuse existing anchor persistence (sidecar row) and anchor resolver on read.

**UI:**
- Reply composer exposes **attach code** flow (same modal / line-pick as top-level composer).
- Nested replies with anchors show `CommentAnchorPreview` and outdated/orphaned badges.
- Fenced Markdown code blocks in reply body remain supported alongside optional anchors.

## Acceptance criteria

- [ ] POST reply with anchor stores ref, commitSha, path, line on the reply row
- [ ] POST reply without anchor still works (markdown-only reply)
- [ ] GET nested list includes anchor on replies; resolver runs on read
- [ ] Anchored reply may reference a different file/line than its root comment
- [ ] Reply composer: code-attach modal integrated on per-root reply form
- [ ] Web UI: anchor preview renders on nested replies
- [ ] API test: anchored reply appears under root in `replies[]` with anchor DTO

## Blocked by

- [11-basic-sub-thread-replies.md](./11-basic-sub-thread-replies.md)

## User stories covered

- 6, 7, 8, 9, 47

## Notes

- Does not add blob-page “reply with anchor” deep link; attach-from-discussion-detail only in this slice.
- Anchor resolver limitations (merge/rename) same as disc-09.
