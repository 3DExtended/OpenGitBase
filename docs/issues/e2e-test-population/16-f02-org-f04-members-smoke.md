<!-- forge: #104 -->

# F02 org + F04 members smoke

## Metadata

- ID: pop-16
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-test-population.md

## Parent

[PRD: E2E Test Population](../../prd/e2e-test-population.md)

## What to build

Greenfield **smoke packs** for organizations (F02) and repository members (F04) — **10 `@Smoke` scenarios each** (20 total).

**F02 minimum:**

1. Create org
2. List my orgs
3. Get org by slug (member)
4. Outsider denied on org admin endpoints
5. Add member
6. Owner promotes member
7. Org invite create + accept
8. Create repo under org namespace
9. Member cannot delete org
10. Last owner cannot leave

**F04 minimum:**

1. Add collaborator as owner
2. List members
3. Change reader → writer
4. Remove member
5. Outsider denied on member CRUD
6. New reader can browse private repo
7. New reader can clone via PAT
8. Admin role assigned
9. Cannot demote last admin
10. Self-remove member

## Acceptance criteria

- [ ] 20 smoke scenarios with baselines
- [ ] Org-owned repo links to git HTTPS smoke
- [ ] Catalog F02 and F04 smoke sections complete
- [ ] New xUnit categories `Organization`, `RepositoryMember` (or equivalent)

## Blocked by

- [02-shared-fixture-library.md](./02-shared-fixture-library.md)
- [04-auth-matrix-theory-runner.md](./04-auth-matrix-theory-runner.md)

## User stories covered

- 38–44, 52–55

## Notes

- Combined slice because org membership and repo members share fixture patterns.
