# Storage content HTTP API

## Metadata

- ID: repo-browse-01
- Type: AFK
- Status: ready
- Source: docs/prd/repository-web-browsing.md

## Parent

[PRD: Repository Web Browsing (File Tree, Blob View, README)](../../prd/repository-web-browsing.md)

## What to build

Extend the storage node internal HTTP server with bearer-authenticated **git read** operations against bare repositories on disk. Use git subprocess commands (`git ls-tree`, `git cat-file`, `git rev-parse`, etc.) — no custom object database implementation.

Expose internal endpoints (conceptual paths under the existing storage HTTP API):

- List local **branches** (branch heads with commit SHAs).
- List **tags**.
- **Tree** listing for a ref + path (empty path = repository root).
- **Blob** metadata and inline text content when under the 1 MB cap.
- **Blob raw** bytes stream with appropriate `Content-Type` and `Content-Disposition`.
- **README** resolution at repository root using GitHub-style precedence (`README.md`, `README.markdown`, `README`, `README.txt`, case-insensitive).

Directory entries include name, path, type (tree/blob), and size where cheaply available. Invalid ref, missing path, or missing repo return appropriate 404 errors.

## Acceptance criteria

- [ ] Bearer token auth required on all new content endpoints (same pattern as existing storage lifecycle API)
- [ ] Branches endpoint returns local branch names and tip SHAs
- [ ] Tags endpoint returns tag names and target SHAs
- [ ] Tree endpoint returns entries for a valid ref + path; root path works
- [ ] Blob endpoint returns text content for a small text file; sets `isBinary` / size metadata correctly
- [ ] Blob endpoint omits inline body and sets `isTooLarge` when content exceeds 1 MB
- [ ] Raw endpoint returns full file bytes for any blob size
- [ ] Readme endpoint returns markdown/text source for `README.md`; 404 when none at root
- [ ] Readme precedence tested: `README.md` wins over `readme.txt` when both exist
- [ ] Invalid bearer token returns 401
- [ ] Automated tests: Python unit/integration tests in repo-storage-layer (or equivalent) covering branches, tree, blob, raw, readme, and error cases against a fixture bare repo

## Blocked by

- None — can start immediately

## User stories covered

- 42 (storage layer)
- 41

## Notes

- Sort order (directories first, then files, alphabetical) may be applied at the API layer in issue 02; storage may return git-native order.
- Coordinate physical path validation with existing storage HTTP path guards.
