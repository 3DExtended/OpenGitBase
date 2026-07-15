<!-- forge: #162 -->

# Default branch persistence and settings

## Metadata

- ID: mr-02
- Type: AFK
- Status: ready
- Source: docs/prd/merge-requests.md

## Parent

[PRD: Merge Requests (Branch Protection, Review, and Server-Side Merge)](../../prd/merge-requests.md)

## What to build

Persist the repository **default branch** and expose it through settings and content APIs.

**Data model:** add nullable `defaultBranchName` on repository.

**Behavior:**

- When refs are fetched and `defaultBranchName` is null, set it using existing resolver logic (`main` → `master` → first branch alphabetically).
- Admin+ may set or change default branch via settings API; value must match an existing branch name when branches exist.
- `GET refs` continues returning `defaultRef`; source of truth is stored value with resolver fallback only when null.
- Support `@default` pattern alias resolution helper for protected branch rules (mr-03).

**API:**

- `GET/PATCH` repository default branch settings (Admin+ for patch).

**Web UI (minimal):**

- Default branch field on repository settings (full Branches section lands in mr-12).

## Acceptance criteria

- [ ] Migration adds `defaultBranchName` nullable column on repositories
- [ ] First refs fetch auto-populates default when unset
- [ ] Admin+ can update default branch to an existing branch name
- [ ] Update rejected when branch name does not exist (when repo has branches)
- [ ] `GET refs` `defaultRef` reflects stored default after set
- [ ] `@default` resolver returns stored default branch name for policy evaluation
- [ ] Open merge requests are not retargeted when default changes (no MR entity yet — document/test in mr-06)
- [ ] Handler and API tests for validation and auto-population

## Blocked by

- None — can start immediately

## User stories covered

- 9, 72, 73, 74, 75

## Notes

- Empty repo may have default set to `main` before branch exists; validation relaxes until first ref sync.
