<!-- forge: #163 -->

# Protected branch and push rule CRUD

## Metadata

- ID: mr-03
- Type: AFK
- Status: ready
- Source: docs/prd/merge-requests.md

## Parent

[PRD: Merge Requests (Branch Protection, Review, and Server-Side Merge)](../../prd/merge-requests.md)

## What to build

Persist and manage **protected branch rules** and **push rules** per repository. API and database only — git enforcement lands in mr-04; settings UI in mr-12.

**Protected branch rule fields:**

- Pattern (`main`, `release/*`, `@default`)
- Block direct push (default true)
- Allowed push roles: Owner, Admin (Maintainer label in UI), Writer (multi-select)
- Allowed push user IDs (explicit repo members)
- Require merge request to merge (default true when protected)
- Required approval count
- Merge permission role threshold (default Writer+)
- Force-push policy enum (deny all, allow allowlisted pushers, platform only)
- Dismiss approvals on new commits (default true)
- Optional locked merge strategy (merge commit, squash, fast-forward)

**Push rule types (v1):**

- Max file size per blob
- Forbidden path globs
- Commit message regex (optional)
- Require DCO Signed-off-by on each commit

**API (Admin+):**

- CRUD protected branch rules
- Attach push rules to rules (or repo scope as designed)

No branches protected by default until configured.

## Acceptance criteria

- [ ] Migrations for protected branch rules and push rules
- [ ] Admin+ can create, list, update, delete rules; non-Admin denied
- [ ] Pattern `@default` resolves via mr-02 helper at evaluation time
- [ ] Allowed roles and user IDs persist and round-trip on read
- [ ] Push rule configs persist as typed rule + JSON payload
- [ ] Repository with no rules allows all existing push behavior (enforcement in mr-04)
- [ ] OpenAPI documents settings endpoints
- [ ] Handler tests for CRUD auth and pattern resolution smoke

## Blocked by

- [02-default-branch-persistence-and-settings.md](./02-default-branch-persistence-and-settings.md)

## User stories covered

- 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45

## Notes

- Platform merge identity constant defined here or in mr-04; referenced by allowlist logic later.
