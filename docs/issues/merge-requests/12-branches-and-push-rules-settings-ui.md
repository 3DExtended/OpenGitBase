<!-- forge: #172 -->

# Branches and push rules settings UI

## Metadata

- ID: mr-12
- Type: AFK
- Status: ready
- Source: docs/prd/merge-requests.md

## Parent

[PRD: Merge Requests (Branch Protection, Review, and Server-Side Merge)](../../prd/merge-requests.md)

## What to build

Repository settings **Branches & push rules** section (Admin+): full UI for policies defined in mr-02 and mr-03.

**UI surfaces:**

- Default branch picker (extends mr-02 minimal field)
- Protected branch rules list with add/edit/delete
- Rule editor: pattern, block direct push, role allowlist (Owner / Maintainer / Writer), member allowlist picker, required approvals, merge permission threshold, force-push policy, dismiss approvals toggle, locked merge strategy
- Push rules editor per rule: max file size, forbidden globs, commit regex, DCO toggle
- Empty state explaining no protection until configured
- Maintainer label for Admin role in role pickers

## Acceptance criteria

- [ ] Admin+ sees Branches & push rules in repo settings; Reader/Writer denied write
- [ ] Create/edit/delete protected branch rule round-trips to API
- [ ] `@default` selectable as pattern with helper text
- [ ] Role and member allowlists persist correctly
- [ ] Push rule fields validate client-side where obvious (non-empty pattern, positive file size)
- [ ] Default branch picker lists branches from refs API
- [ ] Changes reflected on next push enforcement (manual verify with mr-04)

## Blocked by

- [03-protected-branch-and-push-rule-crud.md](./03-protected-branch-and-push-rule-crud.md)
- [06-merge-request-core-api-and-ui-shell.md](./06-merge-request-core-api-and-ui-shell.md)

## User stories covered

- 76, 77, 78, 32–45, 72–74

## Notes

- mr-02 may ship minimal default branch field first; this slice consolidates into full Branches section.
