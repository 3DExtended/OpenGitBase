<!-- forge: #221 -->

# [slice] sow-01 - Detector, window store, openWindows on live snapshot

## Metadata

- Type: AFK
- Status: ready

## Parent

PRD discussion #220

## What to build

End-to-end: detect Unhealthy stretches online and expose **open** windows on the live public snapshot.

1. **Outage window detector** (pure) - given prior open windows + latest group/instance health + clock: open after >=5 min contiguous Unhealthy; merge Healthy gaps <=2 min; close after >2 min Healthy; Unhealthy-only (never Degraded-only); no Overall windows; Data stores single-store naming (Postgres/Redis); partial instance windows when instance Unhealthy and group not Unhealthy.
2. **Window store** - persist window entities (id, scope, group, instanceId?, startedAt, endedAt?, suppressed default false, annotation null); migration; indexes for open + retention.
3. **Aggregator wiring** - after each locked probe/rollup tick, run detector and persist create/update/close; log open/close/merge.
4. **Live snapshot** - extend public status DTO with `openWindows` (non-suppressed, endedAt null), redacted like live instances.
5. **Tests** - detector unit tests for thresholds/merge/close/no-Degraded/no-Overall/Data-stores/partial; aggregator single-tick integration asserts open window on snapshot.

Demo: force a group Unhealthy for >=5 min (mocked clock OK) then `GET /public/status` includes matching open window.

## Acceptance criteria

- [ ] Detector opens only after >=5 min Unhealthy
- [ ] Healthy gaps <=2 min do not split a window; >2 min Healthy closes with endedAt
- [ ] Degraded-only never opens a "down" window; no Overall windows
- [ ] Data stores single-store Unhealthy titles Postgres/Redis
- [ ] Partial instance windows when instance Unhealthy and group not Unhealthy
- [ ] Aggregator under advisory lock persists windows each tick
- [ ] `GET /public/status` includes redacted `openWindows` for open non-suppressed windows
- [ ] Unit/integration tests cover detector rules and snapshot projection
- [ ] Aggregator logs open/close/merge at info/warn

## Blocked by

- None

## User stories covered

- 2, 3, 18, 19, 20, 21, 22 (no backfill; charts unchanged), 23 (open), 24, 30
