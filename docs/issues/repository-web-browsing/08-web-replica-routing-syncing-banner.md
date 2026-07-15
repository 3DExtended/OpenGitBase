<!-- forge: #209 -->

# Web replica routing and syncing banner

## Metadata

- ID: repo-browse-08
- Type: AFK
- Status: ready
- Source: docs/prd/repository-web-browsing.md

## Parent

[PRD: Repository Web Browsing (File Tree, Blob View, README)](../../prd/repository-web-browsing.md)

## What to build

Replace ad-hoc storage node selection from issue 02 with a **web read replica selector**:

- Load replication routing for the repository.
- **Exclude primary** from candidates.
- Select the first **healthy non-primary replica**; try next if unreachable.
- **Never** fall back to primary for web content reads when any non-primary replica exists.
- Fail clearly (503 or equivalent) if no healthy non-primary replica is available.

Include **replication lag** in content API responses when the serving replica’s applied watermark trails the primary (`replicationLag.behind: true`). UI shows a subtle **“Syncing…”** banner on repository browse pages when lag is reported; content still renders.

## Acceptance criteria

- [ ] Web content proxy never calls primary storage when a healthy non-primary replica exists
- [ ] Selector falls back to second non-primary replica when first is unreachable
- [ ] Error returned when no non-primary replica is healthy (no silent primary fallback)
- [ ] API tree/blob/readme responses include lag flag when replica watermark < primary watermark
- [ ] UI banner visible when `replicationLag.behind` is true; hidden when in sync
- [ ] Banner does not block interaction; tree and blob content still display
- [ ] Unit tests for web read replica selector: excludes primary, fallback order, failure when none
- [ ] API controller tests mock routing to assert primary is never selected for content reads
- [ ] Automated test: mock lag response → banner appears; in-sync response → banner hidden

## Blocked by

- [02-public-root-tree-web-ui.md](./02-public-root-tree-web-ui.md)
- [ha-storage-05: Read/write routing](../ha-storage-replication/05-read-write-routing.md)

## User stories covered

- 32, 33, 34, 35, 36, 37

## Notes

- Depends on replication watermark metadata from HA storage work (ha-storage-05+).
- Git read routing (primary or in-sync replica) is unchanged; web browsing uses a separate selector policy.
