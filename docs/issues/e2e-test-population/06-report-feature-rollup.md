<!-- forge: #94 -->

# Report feature rollup dashboard

## Metadata

- ID: pop-06
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-test-population.md

## Parent

[PRD: E2E Test Population](../../prd/e2e-test-population.md)

## What to build

Extend the static HTML report with a **per-feature coverage summary** for large suite runs.

1. **Feature rollup table** in report index: feature domain, passed/failed/skipped counts, link to tier details.
2. Consume scenario **Category** traits or catalog metadata where available.
3. Unit test for HTML structure (extend existing report generator tests).

Verifiable: after a multi-tier runner execution, report shows F01–F12 summary rows even when individual test records are empty (tier-level aggregation minimum).

## Acceptance criteria

- [ ] Report index includes feature/domain summary section
- [ ] Tier summaries roll up into feature buckets where mappable
- [ ] Report generator unit test covers new section
- [ ] Styling consistent with existing report

## Blocked by

- [01-scenario-catalog-authoring-checklist.md](./01-scenario-catalog-authoring-checklist.md)

## User stories covered

- 4, 117

## Notes

- v1 may aggregate by xUnit Category trait; deeper per-scenario stats can follow catalog integration.
