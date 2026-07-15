<!-- forge: #63 -->

# Storage commit read helpers

## Metadata

- ID: cv-01
- Type: AFK
- Status: ready
- Source: docs/prd/commit-change-view.md

## Parent

[PRD: Commit Change View (Clickable Commits & Per-Commit Diff)](../../prd/commit-change-view.md)

## What to build

Extend the storage internal HTTP surface with git operations for single-commit reads, alongside existing merge-range diff and commit-list helpers.

**Interfaces (conceptual):**

- **Resolve SHA** — accept full or abbreviated hash; return canonical 40-character SHA, or distinguish not-found vs ambiguous.
- **Commit metadata** — message (subject + body), author name, authored timestamp, parent SHAs (all parents, ordered).
- **First-parent patch** — unified diff of commit vs its first parent (`git show` semantics); reuse existing unified diff parser and DTO shape used by merge request compare.
- **Root commit tree** — when a commit has no parent, return recursive file path listing at that SHA instead of hunks.
- **Diff statistics** — files changed, insertion count, deletion count (from `--numstat` or equivalent).

**Behavior:**

- Verify objects exist before diff work (`cat-file -e` or equivalent).
- Merge commits diff against first parent only; other parents appear in metadata only.
- Empty patch (metadata-only merge commit) returns valid payload with zero files, not an error.
- Binary files flagged consistently with merge-range diff responses.

Expose via a new internal storage route callable from the API storage client (same pattern as existing content diff/commits endpoints).

## Acceptance criteria

- [ ] Resolve full SHA returns canonical hash for an existing commit
- [ ] Resolve unique prefix returns full SHA; ambiguous prefix returns distinguishable error
- [ ] Unknown SHA returns not-found error suitable for API 404 propagation
- [ ] Metadata includes message, author, authored date, and all parent SHAs
- [ ] Linear commit returns unified diff hunks matching `git show` against first parent
- [ ] Root commit returns file path list (no hunks) with `kind` discriminant in payload
- [ ] Merge commit with no tree change returns empty file list with valid metadata
- [ ] Stats block reports files changed, insertions, deletions for diff commits
- [ ] Storage unit/integration tests cover linear, root, merge, prefix resolve, and ambiguous prefix cases

## Blocked by

- [Git storage proxy](../git-storage-proxy/README.md) — storage nodes and internal HTTP API must be operational

## User stories covered

- 13, 14, 35–38, 37, 40 (storage layer)

## Notes

- Diff file/hunk/line JSON shape must stay compatible with merge request **Changes** DTOs so a shared web diff viewer can consume one schema.
- Parent-picker UI is out of scope; storage only exposes all parent SHAs.
- Performance of very large root-commit trees is not optimized in v1; correctness first.
