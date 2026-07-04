# cv-01 handoff — Storage commit read helpers

- **PRD:** `docs/prd/commit-change-view.md`
- **Work item:** `docs/issues/commit-change-view/01-storage-commit-read-helpers.md`
- **Branch:** `main`

## Acceptance criteria

- Resolve full SHA and unique prefix; ambiguous → error
- Metadata: message, author, authored date, all parents
- First-parent unified diff for linear commits
- Root commit → file path list, `kind: root`
- Merge commit with no tree change → empty files, valid metadata
- Stats: files changed, insertions, deletions
- Storage unit tests + internal HTTP route

## Dependencies

- git-storage-proxy (operational)
