# Blocked users (participation controls)

## Metadata

- ID: disc-06
- Type: AFK
- Status: ready
- Source: docs/prd/repository-discussions.md

## Parent

[PRD: Repository Discussions (Threads, Code Comments, Notifications)](../../prd/repository-discussions.md)

## What to build

**Repository participation blocks:** mute users from creating discussions and commenting without revoking read access.

**Data model:** `repository_blocked_users` — `repositoryId`, `userId`, `blockedByUserId`, `blockedAt`, optional `reason`.

**API:**
- Admin/Owner: block user, unblock user, list blocked users.
- Wire disc-01 participation gate to return deny for blocked users on create discussion and create comment.

**UI:**
- Repository settings page section: blocked users list with unblock action.
- Block action entry point (e.g. from member list or discussion participant menu) — minimal v1: settings-only block via user id/username is acceptable if member picker is deferred.

**Behavior:**
- Blocked user with Reader access can still list and view discussions.
- Blocked user receives clear error (403 with message) on create/comment.

## Acceptance criteria

- [ ] Only Admin/Owner can block and unblock
- [ ] Writer/Reader cannot access block APIs (403)
- [ ] Blocked user can read discussion list and detail
- [ ] Blocked user cannot create discussion
- [ ] Blocked user cannot post comment
- [ ] Unblock restores create/comment ability
- [ ] Settings UI lists blocked users with unblock
- [ ] Participation gate in disc-01 module enforces block
- [ ] API tests for block, unblock, and enforcement matrix

## Blocked by

- [02-discussions-list-create-public-read.md](./02-discussions-list-create-public-read.md)

## User stories covered

- 56, 57, 58, 59

## Notes

- Can parallel disc-03, disc-04, disc-05 after disc-02.
- Block does not affect git access — repo membership remains unchanged.
