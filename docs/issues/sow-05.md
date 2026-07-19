<!-- forge: #225 -->

# [slice] sow-05 - Retention prune and 90-day archive affordance

## Metadata

- Type: AFK
- Status: ready

## Parent

PRD discussion #220

## What to build

Bound storage and expose longer lookback without dumping 90 days on the default page.

1. **Retention prune** - delete/prune window records older than 90 days (aggregator tick or dedicated maintenance); tests for prune boundary.
2. **Archive affordance** - public UI control to load/view windows beyond default 7 days up to 90 via windows API `days=`.
3. **Empty/partial polish** - confirm empty timeline + intact charts; no hourly backfill.

## Acceptance criteria

- [ ] Windows older than 90 days are pruned
- [ ] Public page can request archive lookback up to 90 days without making 90 days the default
- [ ] Default remains ~7 days
- [ ] No backfill from hourly history buckets
- [ ] Tests cover prune and archive fetch behavior

## Blocked by

- #224
- #223


## User stories covered

- 10 (archive), 22 (prune), 27 (empty polish)
