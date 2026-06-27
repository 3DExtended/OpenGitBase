# Changes tab, diff, and review threads

## Metadata

- ID: mr-11
- Type: AFK
- Status: ready
- Source: docs/prd/merge-requests.md

## Parent

[PRD: Merge Requests (Branch Protection, Review, and Server-Side Merge)](../../prd/merge-requests.md)

## What to build

**Changes** tab with unified diff and **line-level review threads**.

**Backend:**

- `GET .../changes` returns diff from mr-05 (`targetBaseSha` vs `sourceHeadSha`)
- Review comments: root + one reply level; anchor `(headCommitSha, filePath, lineNumber, diffSide)`
- Resolve/unresolve on roots (author or Writer+); idempotent
- Outdated flag when anchor not in current diff at HEAD
- `GET/POST` review comments; resolve endpoints

**Frontend:**

- Changes tab with unified diff viewer (syntax-highlighted hunks via shared code block component)
- Click line → add review comment (reuse anchor modal from mr-09)
- Thread UI under diff lines: replies, resolve, collapse resolved, expand on new reply
- Outdated threads collapsed with badge

**Commits tab (minimal):** list commits on source since merge-base (can share storage helper from mr-05).

## Acceptance criteria

- [ ] Changes tab renders unified diff for MR with commits
- [ ] Line comment creates anchored root; one level of replies enforced
- [ ] Reply-to-reply rejected
- [ ] Resolve/unresolve permissions match PRD (root author or Writer+)
- [ ] Resolved thread collapsed by default; new reply expands; badge remains until unresolve
- [ ] Outdated comments flagged and collapsed when diff changes after push
- [ ] DTOs compatible with shared thread component adapter
- [ ] API tests for nesting rules, resolve auth, outdated projection

## Blocked by

- [05-storage-diff-mergeability-merge-execute.md](./05-storage-diff-mergeability-merge-execute.md)
- [10-overview-comments.md](./10-overview-comments.md)

## User stories covered

- 61, 62, 63, 64, 67

## Notes

- Side-by-side diff out of scope; unified only.
- Bundled “submit review” flow out of scope — standalone Approve from mr-07.
