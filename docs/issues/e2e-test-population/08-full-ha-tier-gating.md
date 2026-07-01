# Full-HA tier gating

## Metadata

- ID: pop-08
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-test-population.md

## Parent

[PRD: E2E Test Population](../../prd/e2e-test-population.md)

## What to build

Gate **HA chaos tier** and **`@FullHa` scenarios** behind the full-HA compose profile.

1. Tier 7 (HaChaos) runs only when `--profile full-ha` (or skip with clear report reason on fast profile).
2. `[Trait("FullHa")]` scenarios skip on fast profile with message (like `RequiresCompose` health gate).
3. Document in README: fast profile daily vs full-HA nightly.

Verifiable: fast-profile runner completes without HA quorum failures; full-ha profile runs HA tier.

## Acceptance criteria

- [ ] Tier 7 skipped or gated on fast profile with recorded skip reason
- [ ] `FullHa` trait skip behavior documented
- [ ] README profiles section updated
- [ ] Existing HA smoke test still passes under full-ha

## Blocked by

- None — can start immediately

## User stories covered

- 22, 23

## Notes

- Parallel with pop-02, pop-05.
