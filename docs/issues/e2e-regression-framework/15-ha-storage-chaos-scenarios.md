<!-- forge: #84 -->

# HA storage chaos scenarios

## Metadata

- ID: e2e-15
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-regression-framework.md

## Parent

[PRD: Unified E2E Regression Framework](../../prd/e2e-regression-framework.md)

## What to build

Migrate HA storage shell e2e scenarios into C# tests under full-ha profile:

1. Use chaos helpers to stop one storage node mid-scenario.
2. Assert documented HA semantics from HA storage PRD at git/API boundary — e.g. quorum push still succeeds, read routing behavior, or expected denial when quorum unavailable (match existing shell script intent).
3. Full transcript with cluster action steps + git/API baselines.
4. Parity with `test-ha-storage-e2e.sh` / `test-ha-storage-compose.sh` critical paths.

Retire HA storage shell scripts when baselines committed (final deletion in e2e-20).

## Acceptance criteria

- [ ] Scenarios run under `--profile full-ha` only
- [ ] Stop storage node step logged and reproducible from transcript
- [ ] At least one push and one read assertion under degraded topology
- [ ] Committed baselines for HA scenario artifacts
- [ ] Parity with existing HA shell e2e happy/degraded paths
- [ ] `--filter` can run HA scenarios alone

## Blocked by

- [09-git-facade-https-pat-scenario.md](./09-git-facade-https-pat-scenario.md)
- [14-full-ha-profile-chaos-helpers.md](./14-full-ha-profile-chaos-helpers.md)

## User stories covered

- 32, 33, 34

## Notes

- Prior art: `scripts/test-ha-storage-e2e.sh`, ha-storage-replication issue 12.
- Assert observable git/API outcomes, not internal planner implementation.
