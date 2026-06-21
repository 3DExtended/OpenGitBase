# Three-node fleet foundation

## Metadata

- ID: ha-storage-01
- Type: AFK
- Status: ready
- Source: docs/prd/ha-storage-replication.md

## Parent

[PRD: HA Storage Replication (RF=3)](../../prd/ha-storage-replication.md)

## What to build

Expand the default local fleet to three storage nodes so RF=3 behavior can be developed and tested in every environment. Add a `storage-3` Docker Compose service mirroring the existing storage node pattern (volume, PKI cert/key mounts, enrollment environment). Update dispatcher dependencies to wait for all three storage nodes. Extend fleet bootstrap to generate PKI and enrollment tokens for `storage-3`. Document that repository creation will require three healthy storage nodes once replication ships.

This slice is infrastructure-only — no replication logic yet. Success is demonstrable by starting the full stack and seeing three healthy storage nodes in the API registry.

## Acceptance criteria

- [ ] `storage-3` service exists in Docker Compose with its own repos volume and PKI mounts
- [ ] Dispatchers declare a health dependency on `storage-3` alongside `storage-1` and `storage-2`
- [ ] `bootstrap-fleet.sh` generates enrollment and PKI material for `storage-3` when missing
- [ ] `docker-compose.override.example.yml` (if applicable) includes a placeholder snippet for `storage-3` enrollment
- [ ] README local development section documents the three-node storage requirement
- [ ] Starting the full stack after bootstrap registers three healthy storage nodes visible to the API within two heartbeat intervals

## Blocked by

None — can start immediately.

## User stories covered

- 35 — As a developer running locally, I want the default Docker Compose stack to include three storage nodes, so that RF=3 behavior is exercised without extra setup.
- 36 — As a developer running locally, I want bootstrap and enrollment scripts to provision PKI and enrollment for the third storage node, so that local fleet bootstrap stays one command.
- 37 — As a developer, I want repository creation to fail with a clear error when fewer than three storage nodes are healthy in compose, so that local behavior matches production invariants. *(Gate enforced in slice 02; compose prerequisite delivered here.)*

## Notes

- Does not yet block repository create on `<3` nodes — that lands in slice 02.
- Reuse existing `x-storage-common` anchor and enrollment patterns from `storage-1` / `storage-2`.
