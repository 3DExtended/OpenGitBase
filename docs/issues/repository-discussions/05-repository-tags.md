# Repository tags

## Metadata

- ID: disc-05
- Type: AFK
- Status: ready
- Source: docs/prd/repository-discussions.md

## Parent

[PRD: Repository Discussions (Threads, Code Comments, Notifications)](../../prd/repository-discussions.md)

## What to build

**Repository-scoped tags** for categorizing discussions: catalog, assignments, picker UI, and list filtering.

**Data model:**
- `repository_tags` — `repositoryId`, `name`, optional `color`, `createdAt`.
- `discussion_tag_assignments` — many-to-many.

**Permissions:**
- Writer+ create, rename, delete tags in catalog.
- Admin/Owner manage catalog via repository settings UI.
- Reader+ apply **existing** tags when creating or editing discussions they may edit (creator on own Open/Engaged; Writer+ on any Open/Engaged).
- No tag edits on Resolved/Dismissed until reopen.

**UI:**
- Tag picker on create and edit discussion forms.
- Tags displayed on list rows and detail header.
- List filter by one or more tags.
- Repository settings: tag management section for Admin/Owner.

## Acceptance criteria

- [ ] Writer+ can create and delete repository tags
- [ ] Duplicate tag name per repo rejected
- [ ] Reader+ can assign existing tags on create (Open discussion)
- [ ] Creator can add/remove tags on own Open/Engaged discussion
- [ ] Writer+ can edit tags on any Open/Engaged discussion
- [ ] Tags locked on Resolved/Dismissed discussions
- [ ] Discussion list filter by tag returns matching discussions only
- [ ] Admin/Owner tag management visible in repo settings
- [ ] API tests for catalog CRUD and assignment permissions

## Blocked by

- [02-discussions-list-create-public-read.md](./02-discussions-list-create-public-read.md)

## User stories covered

- 6 (tag filter), 10, 22, 39, 40, 41, 42

## Notes

- Can be implemented in parallel with disc-03 and disc-04 after disc-02 merges.
- Status filter from disc-02 and tag filter should compose in list API.
