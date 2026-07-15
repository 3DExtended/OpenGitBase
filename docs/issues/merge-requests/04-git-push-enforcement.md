<!-- forge: #164 -->

# Git push enforcement

## Metadata

- ID: mr-04
- Type: AFK
- Status: ready
- Source: docs/prd/merge-requests.md

## Parent

[PRD: Merge Requests (Branch Protection, Review, and Server-Side Merge)](../../prd/merge-requests.md)

## What to build

Enforce protected branch rules and push rules at the **git layer** for both HTTPS and SSH entry points.

**Evaluation order on push:** authentication → protected-branch allowlist / block → push rules → accept.

**Responsibilities:**

- Extend `WriteGit` access-check with ref name and old/new SHA (force-push detection).
- Deny direct push to protected refs unless actor is platform merge identity or on role/user allowlist.
- Apply force-push policy per matching rule.
- Reject pushes failing push rules (forbidden paths, max file size, commit message regex, missing DCO) with actionable error messages naming rule and commit.
- Storage receive-pack validation aligned with API decisions (defense in depth).

**Platform merge identity:** dedicated internal credential recognized by enforcement; always allowed to update protected target refs during server-side merge (mr-08).

## Acceptance criteria

- [ ] Writer direct push to protected `main` denied when rule blocks direct push
- [ ] Allowlisted Admin can direct push to protected ref when configured
- [ ] Platform identity can push merge result to protected ref
- [ ] Push with forbidden path glob rejected with clear error
- [ ] Push with missing Signed-off-by rejected when DCO rule enabled
- [ ] Force-push to protected target denied per policy
- [ ] Force-push to unprotected MR source branch allowed (approval dismiss wired in mr-07)
- [ ] HTTPS and SSH access-check paths behave identically in tests
- [ ] Integration test: protect branch → git push denied → push to feature branch succeeds

## Blocked by

- [03-protected-branch-and-push-rule-crud.md](./03-protected-branch-and-push-rule-crud.md)

## User stories covered

- 39, 46, 47, 48, 89, 90

## Notes

- MR-only merge path enforced here: non-platform pushes to protected target denied even for Writer when `requireMergeRequest` is set.
