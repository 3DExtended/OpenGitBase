<!-- forge: #188 -->

# Discussion detail, assignee, and Writer close actions

## Metadata

- ID: disc-03
- Type: AFK
- Status: ready
- Source: docs/prd/repository-discussions.md

## Parent

[PRD: Repository Discussions (Threads, Code Comments, Notifications)](../../prd/repository-discussions.md)

## What to build

Complete the **discussion lifecycle** for Writer+ close actions and metadata editing on the detail page.

**Status transitions (API + UI):**
- Writer+ may **resolve** a discussion (from Engaged — Engaged only reachable after disc-04; until then resolve from Open is acceptable for incremental testing or return 409 if not Engaged).
- Writer+ may **dismiss** from **Open** or **Engaged**.
- No standalone reopen endpoint (reopen via comment in disc-04).

**Assignee:**
- Optional single assignee; must be a repository member with read access.
- Creator or Writer+ can set/change assignee while Open or Engaged.

**Metadata edit rules:**
- Creator may edit title while Open or Engaged.
- Writer+ may edit title and assignee on any Open or Engaged discussion.
- Resolved/Dismissed: title and assignee locked until reopen (disc-04).

**UI:**
- Status badges: Open and Engaged visually distinct from Resolved and Dismissed.
- Writer+ actions: Resolve and Dismiss buttons with confirmation.
- Assignee picker on detail and create (if not already on create form).

## Acceptance criteria

- [ ] Resolve transitions Engaged → Resolved (Writer+ only)
- [ ] Dismiss transitions Open → Dismissed and Engaged → Dismissed (Writer+ only)
- [ ] Reader cannot resolve or dismiss (403)
- [ ] Assignee optional; clearing assignee allowed
- [ ] Assignee must be valid repo member; invalid assignee rejected
- [ ] Creator can edit title on Open discussion; cannot edit on Resolved/Dismissed
- [ ] Writer+ can edit title and assignee on Open/Engaged discussions
- [ ] Detail page shows assignee, status badge, and Writer+ action buttons
- [ ] API tests for resolve, dismiss, assignee, and metadata edit permissions
- [ ] `updatedAt` bumps on metadata and status changes

## Blocked by

- [02-discussions-list-create-public-read.md](./02-discussions-list-create-public-read.md)

## User stories covered

- 11, 13, 17, 18, 19, 22, 23, 24, 43, 61

## Notes

- Tag edit rules depend on disc-05; title-only edits here are fine until tags ship.
- Assignee auto-subscribe deferred to disc-07.
