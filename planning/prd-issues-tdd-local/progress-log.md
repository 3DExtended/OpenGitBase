# Progress log — Git HTTPS PAT

## 2026-06-18

### Setup

- Wrote issue files to `docs/issues/git-https-pat/` (8 slices + README)
- Orchestration: single branch `feat/git-https-pat`, sequential, no parallelism
### git-https-01 — completed

- Backend PAT feature, git config API, migration `AddGitAccessTokenEntity`
- Web PAT settings page at `/settings/access-tokens`
- All targeted unit/API tests green on `feat/git-https-pat`

### git-https-02 — completed

- Access-check accepts PAT or SSH key (mutually exclusive)
- Token scope enforcement (read denies write-git)
- `StorageNodeInternalGitHttpPort` in routing response
- Migration `AddStorageNodeInternalGitHttpPort`, storage-agent registration field

### git-https-03 — completed

- nginx + fcgiwrap + git-http-backend on internal :8082
- Standalone integration test: provision → clone over Smart HTTP
- Compose healthcheck includes git HTTP process

### git-https-04 — completed

- Kestrel Smart HTTP on dispatcher :8082 (`--serve-http`)
- PAT via Basic auth → access-check → reverse proxy to storage git HTTP
- Path parser + proxy unit tests; smoke script for compose verification

### Next

- **git-https-05** — HAProxy unified HTTP routing + Cloudflare tunnel
