<!-- forge: #86 -->

# Discussion E2E scenarios

## Metadata

- ID: e2e-18
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-regression-framework.md

## Parent

[PRD: Unified E2E Regression Framework](../../prd/e2e-regression-framework.md)

## What to build

Migrate discussion shell e2e into C# tests under `Discussion/` feature folder. Parity with `test-discussions-e2e.sh` and disc-10 integration scope:

1. **Public repo** — anonymous read list/detail; create/comment requires auth.
2. **Private repo** — anonymous 404; outsider 403; member create and read.
3. **Lifecycle** — Open → Engaged on first non-creator comment; Writer+ resolve; reopen via comment → Open without re-Engage.
4. **Blocking** — blocked user reads but cannot create/comment; unblock restores participation.
5. **Notifications** — in-app notification on comment; captured email subject contains `[owner/repo #n]` prefix (normalized in baseline).
6. **Tags** — filter list by tag (smoke).
7. **Anchors** — anchored comment smoke on git-seeded fixture repo (located or outdated state).

Full transcripts + committed API/email baselines. Retire `test-discussions-e2e.sh` when done (e2e-20).

## Acceptance criteria

- [ ] Public anonymous read passes; unauthenticated create fails
- [ ] Private anonymous 404 and outsider 403 pass
- [ ] Engaged once + reopen without re-Engage pass
- [ ] Block enforced; unblock restores write
- [ ] Email subject format asserted via capture sink (normalized baseline)
- [ ] Anchor smoke on fixture repository with git-seeded content
- [ ] Committed baselines for discussion scenarios
- [ ] Parity with existing discussion shell script scenarios

## Blocked by

- [07-capturing-email-sender-e2e-api.md](./07-capturing-email-sender-e2e-api.md)
- [08-identity-seed-auth-journey.md](./08-identity-seed-auth-journey.md)

## User stories covered

- Discussion PRD integration scenarios (disc-10): public/private access, lifecycle, blocking, notifications, tags, anchors

## Notes

- Prior art: `scripts/test-discussions-e2e.sh`, [disc-10](../repository-discussions/10-e2e-discussion-integration-tests.md).
- Anchor scenario may require git push fixture setup — coordinate with git facade or inline push in test setup.
- Parallel track after e2e-08; does not require e2e-09 unless anchor needs remote repo content.
