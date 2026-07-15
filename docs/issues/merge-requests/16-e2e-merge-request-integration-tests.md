<!-- forge: #176 -->

# End-to-end merge request integration tests

## Metadata

- ID: mr-16
- Type: AFK
- Status: ready
- Source: docs/prd/merge-requests.md

## Parent

[PRD: Merge Requests (Branch Protection, Review, and Server-Side Merge)](../../prd/merge-requests.md)

## What to build

Docker Compose **integration test script** (and supporting API tests if needed) covering the primary merge request scenarios from the PRD.

**Scenarios:**

1. Unprotected repo: direct push still works; MR optional
2. Protect `main`: Writer direct push denied; feature branch push allowed
3. Allowlisted Admin direct push to protected ref succeeds
4. Push with forbidden path / missing DCO rejected when rules enabled
5. Create MR Draft → publish Open → approvals → Approved → squash merge → Merged
6. MR with `closes` link resolves discussion on merge
7. Approved MR with target advance causing conflict: merge disabled until fixed locally
8. Public repo: anonymous read MR; unauthenticated create returns 401
9. Private repo: anonymous 404; outsider 403
10. Force-push to MR source dismisses approvals

Follow patterns from `scripts/e2e-https-git-test.sh` and discussion e2e (disc-10).

## Acceptance criteria

- [ ] Single script runnable in CI/local compose documents prerequisites
- [ ] Scenarios 2, 5, and 6 pass reliably (core happy path)
- [ ] Auth matrix spot-checks (public/private) included
- [ ] Push rule rejection assertion with error message substring
- [ ] Script exits non-zero on failure with clear step labels
- [ ] README or issues index references how to run MR e2e

## Blocked by

- [08-server-side-merge-and-discussion-closes-links.md](./08-server-side-merge-and-discussion-closes-links.md)
- [11-changes-tab-diff-and-review-threads.md](./11-changes-tab-diff-and-review-threads.md)
- [14-merge-request-notifications.md](./14-merge-request-notifications.md)

## User stories covered

- Integration scenarios from PRD Testing Decisions (all listed scenarios)

## Notes

- Diff/review UI not fully exercised in script — API-level review comment smoke optional.
- Notification email deliverability not asserted — in-app or API notification record check sufficient.

## Running MR e2e locally

With the docker compose stack up:

```bash
./scripts/test-merge-requests-e2e.sh
```

Optional env: `API_URL`, `MERGE_REQUEST_E2E_SUFFIX`, `MERGE_REQUEST_REPO`.
