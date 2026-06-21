# Repository Web Browsing — implementation issues

Vertical slices for [PRD: Repository Web Browsing (File Tree, Blob View, README)](../../prd/repository-web-browsing.md).

Implement in dependency order on a feature branch (e.g. `feat/repository-web-browsing`); each issue is blocked by the ones listed in its file.

| # | ID | Issue | Type | Blocked by |
|---|-----|-------|------|------------|
| 1 | `repo-browse-01` | [Storage content HTTP API](./01-storage-content-http-api.md) | AFK | — |
| 2 | `repo-browse-02` | [Public root tree in the web UI](./02-public-root-tree-web-ui.md) | AFK | 1 |
| 3 | `repo-browse-03` | [Private repository content authorization](./03-private-repository-content-authorization.md) | AFK | 2 |
| 4 | `repo-browse-04` | [Branch/tag ref picker and tree navigation](./04-branch-tag-ref-picker-tree-navigation.md) | AFK | 2 |
| 5 | `repo-browse-05` | [README on repository home](./05-readme-on-repository-home.md) | AFK | 2, 4 |
| 6 | `repo-browse-06` | [Blob view — text, download, size cap](./06-blob-view-text-download-size-cap.md) | AFK | 4 |
| 7 | `repo-browse-07` | [Blob preview — images, SVG, markdown toggle](./07-blob-preview-images-markdown-toggle.md) | AFK | 6 |
| 8 | `repo-browse-08` | [Web replica routing and syncing banner](./08-web-replica-routing-syncing-banner.md) | AFK | 2, [ha-storage-05](../ha-storage-replication/05-read-write-routing.md) |
| 9 | `repo-browse-09` | [Redis cache, cache headers, anonymous rate limits](./09-redis-cache-rate-limits.md) | AFK | 3 |
| 10 | `repo-browse-10` | [Empty repository state and collapsible clone](./10-empty-repo-collapsible-clone.md) | AFK | 2 |
| 11 | `repo-browse-11` | [End-to-end repository browse integration tests](./11-e2e-repository-browse-integration-tests.md) | AFK | 3, 5, 6, 7, 8, 9 |
