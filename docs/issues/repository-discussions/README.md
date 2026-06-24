# Repository Discussions — implementation issues

Vertical slices for [PRD: Repository Discussions (Threads, Code Comments, Notifications)](../../prd/repository-discussions.md) and [PRD: Discussion Sub-Threads](../../prd/discussion-sub-threads.md).

Implement in dependency order on a feature branch (e.g. `feat/repository-discussions`); each issue is blocked by the ones listed in its file.

## Core discussions (disc-01 – disc-10)

| # | ID | Issue | Type | Status | Blocked by |
|---|-----|-------|------|--------|------------|
| 1 | `disc-01` | [Discussion authorization](./01-discussion-authorization.md) | AFK | ready | — |
| 2 | `disc-02` | [Discussions list, create, and public read](./02-discussions-list-create-public-read.md) | AFK | ready | 1 |
| 3 | `disc-03` | [Discussion detail, assignee, and Writer close actions](./03-discussion-detail-assignee-writer-close.md) | AFK | ready | 2 |
| 4 | `disc-04` | [Thread comments and engagement lifecycle](./04-thread-comments-engagement-lifecycle.md) | AFK | ready | 3 |
| 5 | `disc-05` | [Repository tags](./05-repository-tags.md) | AFK | ready | 2 |
| 6 | `disc-06` | [Blocked users (participation controls)](./06-blocked-users-participation-controls.md) | AFK | ready | 2 |
| 7 | `disc-07` | [Mentions, subscriptions, and in-app notifications](./07-mentions-subscriptions-in-app-notifications.md) | AFK | ready | 4 |
| 8 | `disc-08` | [Email notifications](./08-email-notifications.md) | AFK | ready | 7 |
| 9 | `disc-09` | [Anchored code comments](./09-anchored-code-comments.md) | AFK | ready | 4, [repo-browse-06](../repository-web-browsing/06-blob-view-text-download-size-cap.md) |
| 10 | `disc-10` | [End-to-end discussion integration tests](./10-e2e-discussion-integration-tests.md) | AFK | ready | 3, 4, 6, 7, 8, 9 |

## Sub-threads (disc-11 – disc-16)

| # | ID | Issue | Type | Status | Blocked by |
|---|-----|-------|------|--------|------------|
| 11 | `disc-11` | [Basic sub-thread replies](./11-basic-sub-thread-replies.md) | AFK | ready | 4, 9 |
| 12 | `disc-12` | [Anchored replies](./12-anchored-replies.md) | AFK | ready | 11 |
| 13 | `disc-13` | [Sub-thread resolve and collapse UI](./13-sub-thread-resolve-collapse.md) | AFK | ready | 11 |
| 14 | `disc-14` | [Orphan replies after root soft-delete](./14-orphan-replies.md) | AFK | ready | 11 |
| 15 | `disc-15` | [Sub-thread resolve notifications](./15-sub-thread-resolve-notifications.md) | AFK | ready | 7, 13 |
| 16 | `disc-16` | [Sub-thread integration tests](./16-sub-thread-integration-tests.md) | AFK | ready | 6, 11, 12, 13, 14, 15 |

## Dependency graph

```
disc-01 ──► disc-02 ──┬──► disc-03 ──► disc-04 ──► disc-07 ──► disc-08
                      ├──► disc-05
                      └──► disc-06

disc-04 + repo-browse-06 ──► disc-09

disc-03, disc-04, disc-06, disc-07, disc-08, disc-09 ──► disc-10

disc-04, disc-09 ──► disc-11 ──┬──► disc-12
                                ├──► disc-13 ──► disc-15 (also disc-07)
                                └──► disc-14

disc-06, disc-11, disc-12, disc-13, disc-14, disc-15 ──► disc-16
```

## Source

- [docs/prd/repository-discussions.md](../../prd/repository-discussions.md)
- [docs/prd/discussion-sub-threads.md](../../prd/discussion-sub-threads.md)
