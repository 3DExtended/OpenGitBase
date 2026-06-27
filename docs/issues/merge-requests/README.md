# Merge Requests — implementation issues

Vertical slices for [PRD: Merge Requests (Branch Protection, Review, and Server-Side Merge)](../../prd/merge-requests.md).

Implement in dependency order on a feature branch (e.g. `feat/merge-requests`); each issue is blocked by the ones listed in its file.

| # | ID | Issue | Type | Status | Blocked by |
|---|-----|-------|------|--------|------------|
| 1 | `mr-01` | [Merge request authorization](./01-merge-request-authorization.md) | AFK | ready | — |
| 2 | `mr-02` | [Default branch persistence and settings](./02-default-branch-persistence-and-settings.md) | AFK | ready | — |
| 3 | `mr-03` | [Protected branch and push rule CRUD](./03-protected-branch-and-push-rule-crud.md) | AFK | ready | 2 |
| 4 | `mr-04` | [Git push enforcement](./04-git-push-enforcement.md) | AFK | ready | 3 |
| 5 | `mr-05` | [Storage diff, mergeability, and merge execute](./05-storage-diff-mergeability-merge-execute.md) | AFK | ready | [git-storage-proxy](../git-storage-proxy/README.md) |
| 6 | `mr-06` | [Merge request core (API + list, create, detail shell)](./06-merge-request-core-api-and-ui-shell.md) | AFK | ready | 1, 2 |
| 7 | `mr-07` | [Approvals and merge gates](./07-approvals-and-merge-gates.md) | AFK | ready | 6 |
| 8 | `mr-08` | [Server-side merge and discussion closes links](./08-server-side-merge-and-discussion-closes-links.md) | AFK | ready | 4, 5, 7 |
| 9 | `mr-09` | [Shared collaboration UI components](./09-shared-collaboration-ui-components.md) | AFK | ready | [disc-04](../repository-discussions/04-thread-comments-engagement-lifecycle.md) |
| 10 | `mr-10` | [Overview comments](./10-overview-comments.md) | AFK | ready | 6, 9 |
| 11 | `mr-11` | [Changes tab, diff, and review threads](./11-changes-tab-diff-and-review-threads.md) | AFK | ready | 5, 10 |
| 12 | `mr-12` | [Branches and push rules settings UI](./12-branches-and-push-rules-settings-ui.md) | AFK | ready | 3, 6 |
| 13 | `mr-13` | [Post-push create banner](./13-post-push-create-banner.md) | AFK | ready | 2, 6 |
| 14 | `mr-14` | [Merge request notifications](./14-merge-request-notifications.md) | AFK | ready | 6, [disc-07](../repository-discussions/07-mentions-subscriptions-in-app-notifications.md), [disc-08](../repository-discussions/08-email-notifications.md) |
| 15 | `mr-15` | [Linked discussions sidebar](./15-linked-discussions-sidebar.md) | AFK | ready | 6, 8 |
| 16 | `mr-16` | [End-to-end merge request integration tests](./16-e2e-merge-request-integration-tests.md) | AFK | ready | 8, 11, 14 |

**First demo milestone:** complete **mr-08** — protect `main`, push a feature branch, open an MR, approve, and merge (diff review lands in **mr-11**).

## Dependency graph

```
mr-01 ──┐
mr-02 ──┼──► mr-06 ──► mr-07 ──► mr-08 ──► mr-16
        │       │                      ▲
mr-03 ──┼──► mr-04 ───────────────────┤
        │       │                      │
        ├──► mr-12    mr-05 ──► mr-11 ─┘
        └──► mr-13

disc-04 ──► mr-09 ──► mr-10 ──► mr-11

mr-06 + disc-07/08 ──► mr-14 ──► mr-16
mr-06 + mr-08 ──► mr-15
```

## Source

- [docs/prd/merge-requests.md](../../prd/merge-requests.md)
