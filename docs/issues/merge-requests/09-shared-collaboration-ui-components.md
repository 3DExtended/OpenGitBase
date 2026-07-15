<!-- forge: #169 -->

# Shared collaboration UI components

## Metadata

- ID: mr-09
- Type: AFK
- Status: ready
- Source: docs/prd/merge-requests.md

## Parent

[PRD: Merge Requests (Branch Protection, Review, and Server-Side Merge)](../../prd/merge-requests.md)

## What to build

Extract **shared presentation components** from the discussions UI into a neutral collaboration layer usable by both discussions and merge requests.

**Components to extract/adapt (examples):**

- Markdown editor (composer)
- Rendered Markdown body with syntax-highlighted code blocks
- Comment thread list layout
- Sub-thread reply UI with resolve/collapse header
- Code-attach / git anchor modal
- @mention autocomplete wrapper
- Status badge chip (parameterized colors/labels)

**Location:** e.g. `components/collaboration/` — neither feature imports the other's page shells.

**Adapter pattern:** thin mappers from `DiscussionCommentDto` and future `MergeRequestCommentDto` into shared thread props (`replyCount`, `isResolved`, `isOutdated`, `author`, `bodyHtml`, etc.).

Refactor existing discussion pages to use extracted components without visual regression (behavior unchanged).

## Acceptance criteria

- [ ] Shared Markdown editor used by discussion comment composer (existing flows still work)
- [ ] Shared rendered body used by discussion detail
- [ ] Sub-thread resolve/collapse UI uses shared thread component
- [ ] Discussion pages refactored; no duplicate editor/renderer in discussion feature folder
- [ ] Exported TypeScript props documented for MR adapters in mr-10/mr-11
- [ ] Existing discussion UI smoke/manual checklist passes (or component tests where present)

## Blocked by

- [04-thread-comments-engagement-lifecycle.md](../repository-discussions/04-thread-comments-engagement-lifecycle.md) — discussion comment UI must exist to extract from

## User stories covered

- 60, 65, 66 (foundation for MR comment UI)

## Notes

- Can proceed in parallel with mr-06 backend if discussion UI is stable; MR slices mr-10+ hard-depend on this.
- Do not move discussion-specific business logic into shared layer — presentation only.
