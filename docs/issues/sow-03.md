<!-- forge: #224 -->

# [slice] sow-03 - Public /status outage timeline UI

## Metadata

- Type: AFK
- Status: ready

## Parent

PRD discussion #220

## What to build

Public timeline as the primary history story on `/status`.

1. **Timeline section** - below live groups / above demoted %; render open + last 7 days from snapshot + windows API; copy "{Group} down since ..." / "{Group} down start-end"; duration side metadata; annotations when present.
2. **Expand** - instance detail for group windows (redacted labels).
3. **Partial issues** - secondary section for partial/instance windows.
4. **Demote %** - uptime callout secondary; keep charts.
5. **Timezone** - UTC default + local toggle for displayed times.
6. **Empty state** - clear copy when no windows yet; banner stacking unchanged (banner above).
7. **Visual snapshots** - Playwright for timeline fixtures (open, closed, partial, empty).

## Acceptance criteria

- [ ] Timeline visible on `/status` and leads history vs demoted %
- [ ] Open and closed copy matches PRD templates; duration secondary
- [ ] Expand shows instance detail; Partial issues section present when applicable
- [ ] UTC default with local timezone toggle
- [ ] Empty state is clear; charts and banner behavior preserved
- [ ] Visual snapshots cover key timeline states

## Blocked by

- #222


## User stories covered

- 1, 6, 7, 12, 13, 14, 15, 27, 29 (public)
