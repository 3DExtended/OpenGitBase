<!-- forge: #83 -->

# Full-HA compose profile + chaos helpers

## Metadata

- ID: e2e-14
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-regression-framework.md

## Parent

[PRD: Unified E2E Regression Framework](../../prd/e2e-regression-framework.md)

## What to build

Extend compose orchestration for HA testing and failure injection:

1. **`--profile full-ha`** — boots production-like topology (multi-storage, multi-dispatcher, multi-api per existing compose).
2. **IClusterChaos** — `StopServiceAsync`, `StartServiceAsync`, `RestoreAllAsync` for named compose services.
3. **Transcript integration** — cluster actions auto-logged as human-readable steps ("Stopped storage-2").
4. **Filter support** — `--filter` can run chaos-tagged tests without full fast suite (document pattern).

Deliver one smoke chaos test under full-ha profile: stop non-critical service → assert health degradation or recovery path → restore all.

## Acceptance criteria

- [ ] `--profile full-ha` starts full HA stack and passes health wait
- [ ] Chaos helper stops and restores at least one storage or dispatcher service
- [ ] Cluster actions appear in operation transcript
- [ ] `RestoreAll` returns stack to healthy state for subsequent tests
- [ ] `--filter` can target chaos tests independently
- [ ] Fast profile remains default unchanged

## Blocked by

- [01-runner-skeleton-fast-compose-tier0.md](./01-runner-skeleton-fast-compose-tier0.md)

## User stories covered

- 3, 9, 33, 34, 35, 36

## Notes

- Parallel track from e2e-01 — does not block auth/git slices on critical path.
- HA behavioral scenarios land in e2e-15.
