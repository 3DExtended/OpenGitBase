# Execution plan: Status outage windows and headlines

**Source PRD:** [docs/prd/status-outage-windows-and-headlines.md](../../../docs/prd/status-outage-windows-and-headlines.md)  
**Work items:** [docs/issues/status-outage-windows-and-headlines/README.md](../../../docs/issues/status-outage-windows-and-headlines/README.md)  
**Parent forge:** [#220](https://api.opengitbase.com/opengitbase/open-git-base/discussions/220)

Branch strategy: **main** (all work items committed sequentially on default branch).

## Dependency order

| Order | ID | Title | Type | Status | Blocked by | Forge |
|-------|-----|-------|------|--------|------------|-------|
| 1 | sow-01 | Detector, store, openWindows on snapshot | AFK | in progress | — | #221 |
| 2 | sow-02 | Public windows history API | AFK | ready | sow-01 | #222 |
| 3 | sow-04 | Admin suppress and annotate windows | AFK | ready | sow-01 | #223 |
| 4 | sow-03 | Public status outage timeline UI | AFK | ready | sow-02 | #224 |
| 5 | sow-05 | Retention prune and 90-day archive | AFK | ready | sow-03, sow-04 | #225 |

Note: sow-04 can run after sow-01 in parallel with sow-02/03; sequential order here runs sow-02 then sow-04 then sow-03 then sow-05 for simpler UI dependency (admin UI independent of public timeline; sow-05 needs both).

Adjusted sequential order for this run:

1. sow-01 → 2. sow-02 → 3. sow-04 → 4. sow-03 → 5. sow-05

(sow-03 after sow-04 so public timeline can show annotations from the start of UI work; either order of 03/04 after 02 is valid.)

## Dependency graph

```
sow-01 → sow-02 → sow-03 ─┐
       └→ sow-04 ─────────┴→ sow-05
```

## User confirmation

User invoked `/prd-issues-tdd-local-main` with instruction to complete all 5 issues — plan approved; execution started.
