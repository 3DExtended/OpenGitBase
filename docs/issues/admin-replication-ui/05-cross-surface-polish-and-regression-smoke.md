<!-- forge: #33 -->

# Cross-surface polish and regression smoke

## Metadata

- ID: admin-repl-05
- Type: AFK
- Status: ready
- Source: docs/prd/admin-replication-ui.md

## Parent

[PRD: Admin Replication UI](../../prd/admin-replication-ui.md)

## What to build

Final integration pass across admin replication surfaces:

1. **Shared UI helpers** — extract reusable pieces introduced in slices 02–04 where duplicated: 30-second poll composable, replication state badge mapping (colors/labels), provisioning and sync progress bar helpers.
2. **Cross-link consistency** — verify storage teaser → index filter URLs, index → detail → back navigation, and admin home tiles all align.
3. **Attention alignment check** — confirm teaser repos match list API `attention` semantics (manual or automated smoke); fix any client/server drift.
4. **Regression smoke** — optional Playwright or shell test that admin replication routes render for an admin session; at minimum document manual smoke checklist in issue notes or planning log.
5. **Documentation touch-up** — mark ha-storage-11 acceptance gaps resolved in planning notes if applicable; ensure README index statuses remain accurate.

No new product features — polish and confidence only.

## Acceptance criteria

- [ ] Duplicated poll/badge/progress logic consolidated into shared helpers where practical
- [ ] Teaser “View all” and index filter chips hit the same server attention rules
- [ ] All three surfaces (storage card, index, detail) refresh correctly without leaking intervals
- [ ] Admin replication smoke passes (automated or documented manual checklist completed)
- [ ] No placeholder replication API hints remain anywhere in admin UI

## Blocked by

- [02-storage-page-fleet-replication-card.md](./02-storage-page-fleet-replication-card.md)
- [03-admin-navigation-and-repository-index.md](./03-admin-navigation-and-repository-index.md)
- [04-repository-replication-detail-page.md](./04-repository-replication-detail-page.md)

## User stories covered

- 5 — Teaser severity consistent with index (verification)
- 6 — Teaser → index navigation (verification)
- 7 — Storage auto-refresh verified
- 12 — Filter chips aligned with server presets (verification)
- 17 — Index auto-refresh verified
- 23 — Detail auto-refresh verified

## Notes

- Lightweight slice by design; skip if slices 02–04 already share helpers cleanly — still run smoke checklist before marking complete.
