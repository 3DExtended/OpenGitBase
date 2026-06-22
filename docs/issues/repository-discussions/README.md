# Repository Discussions — implementation issues

Vertical slices for [PRD: Repository Discussions (Threads, Code Comments, Notifications)](../../prd/repository-discussions.md).

Implement in dependency order on a feature branch (e.g. `feat/repository-discussions`); each issue is blocked by the ones listed in its file.

| # | ID | Issue | Type | Blocked by |
|---|-----|-------|------|------------|
| 1 | `disc-01` | [Discussion authorization](./01-discussion-authorization.md) | AFK | — |
| 2 | `disc-02` | [Discussions list, create, and public read](./02-discussions-list-create-public-read.md) | AFK | 1 |
| 3 | `disc-03` | [Discussion detail, assignee, and Writer close actions](./03-discussion-detail-assignee-writer-close.md) | AFK | 2 |
| 4 | `disc-04` | [Thread comments and engagement lifecycle](./04-thread-comments-engagement-lifecycle.md) | AFK | 3 |
| 5 | `disc-05` | [Repository tags](./05-repository-tags.md) | AFK | 2 |
| 6 | `disc-06` | [Blocked users (participation controls)](./06-blocked-users-participation-controls.md) | AFK | 2 |
| 7 | `disc-07` | [Mentions, subscriptions, and in-app notifications](./07-mentions-subscriptions-in-app-notifications.md) | AFK | 4 |
| 8 | `disc-08` | [Email notifications](./08-email-notifications.md) | AFK | 7 |
| 9 | `disc-09` | [Anchored code comments](./09-anchored-code-comments.md) | AFK | 4, [repo-browse-06](../repository-web-browsing/06-blob-view-text-download-size-cap.md) |
| 10 | `disc-10` | [End-to-end discussion integration tests](./10-e2e-discussion-integration-tests.md) | AFK | 3, 4, 6, 7, 8, 9 |

## Dependency graph

```
disc-01 ──► disc-02 ──┬──► disc-03 ──► disc-04 ──► disc-07 ──► disc-08
                      ├──► disc-05
                      └──► disc-06

disc-04 + repo-browse-06 ──► disc-09

disc-03, disc-04, disc-06, disc-07, disc-08, disc-09 ──► disc-10
```

## Source

[docs/prd/repository-discussions.md](../../prd/repository-discussions.md)
