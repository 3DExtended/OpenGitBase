# Overview comments

## Metadata

- ID: mr-10
- Type: AFK
- Status: ready
- Source: docs/prd/merge-requests.md

## Parent

[PRD: Merge Requests (Branch Protection, Review, and Server-Side Merge)](../../prd/merge-requests.md)

## What to build

**Overview comments** on merge requests: general conversation not tied to diff lines.

**Backend:**

- `merge_request_comments` table (overview variant: no anchor, no parent)
- CRUD: create, list chronological, edit own, soft-delete own; Writer+ soft-delete any
- Markdown pipeline aligned with discussions (sanitize output; no raw HTML in source)
- @mention parse and notification hook (full notification in mr-14)
- Authorization: Reader+ participate (not blocked); wire mr-01

**Frontend:**

- Overview tab comment list + composer using mr-09 shared components
- Edit/delete affordances matching discussion comments

## Acceptance criteria

- [ ] Authenticated Reader+ can post overview comment on Open/Approved/Draft MR
- [ ] Anonymous cannot comment on public repo
- [ ] Markdown renders safely; code blocks highlighted
- [ ] Author can edit with edited indicator; soft-delete own comment
- [ ] Writer+ can soft-delete any comment
- [ ] Comments listed chronologically on Overview tab
- [ ] Mention parsing stores targets for mr-14 notifications
- [ ] API tests for auth matrix and soft-delete

## Blocked by

- [06-merge-request-core-api-and-ui-shell.md](./06-merge-request-core-api-and-ui-shell.md)
- [09-shared-collaboration-ui-components.md](./09-shared-collaboration-ui-components.md)

## User stories covered

- 60, 65, 66

## Notes

- Review comments with anchors land in mr-11 (same table with discriminator or separate — implementer choice, document in handler).
