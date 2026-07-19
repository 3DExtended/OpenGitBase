<!-- forge: #223 -->

# [slice] sow-04 - Admin suppress and annotate windows

## Metadata

- Type: AFK
- Status: ready

## Parent

PRD discussion #220

## What to build

Operator controls for false positives and planned-work notes.

1. **Admin APIs** - list windows (including suppressed); suppress/unsuppress; set/clear public annotation; times not writable; admin role required.
2. **Admin UI** - extend `/admin/status` with window list beside existing banner; suppress toggle; annotation field; link to public `/status`.
3. **Public effect** - suppressed omitted from snapshot openWindows and windows API; annotation appears on public timeline when set.
4. **Visual snapshots** - admin window controls.

## Acceptance criteria

- [ ] Admin can suppress/unsuppress and set/clear annotation
- [ ] Non-admin cannot mutate windows
- [ ] Start/end not editable via admin API/UI
- [ ] Suppressed hidden publicly; still listed in admin
- [ ] Annotation visible on public window when set
- [ ] `/admin/status` shows window controls and link to public page
- [ ] API + visual tests cover above

## Blocked by

- #221


## User stories covered

- 16, 25, 26, 28, 29 (admin)
