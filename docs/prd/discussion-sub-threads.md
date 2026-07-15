<!-- forge: #10 -->

# PRD: Discussion Sub-Threads (Replies and Per-Comment Resolution)

## Problem Statement

Repository discussions today present a **flat chronological list** of comments. Each comment stands alone; there is no way to continue a conversation on a specific note or anchored code comment without posting another top-level entry. Reviewers cannot attach follow-up notes and additional code references to an existing comment in a structured way, and maintainers cannot mark an individual code note or question as addressed without closing the **entire discussion**.

Developers reviewing code expect **sub-threads**: a top-level comment (especially one anchored to a line) acts as the topic, replies accumulate underneath, and that branch can be marked resolved when the note is addressed — while the parent discussion stays open if other topics remain active. Without sub-threads, busy discussions become hard to scan, resolved notes compete visually with open ones, and discussion-level **Resolved** is too coarse a tool for multi-topic threads.

This PRD extends [Repository Discussions (Threads, Code Comments, Notifications)](./repository-discussions.md). It supersedes that PRD's v1 deferral of comment threading with a deliberately narrow model: **one level of replies** and **per-root resolution**, independent of discussion-level lifecycle.

## Solution

Add **sub-threads** to repository discussions:

- Each **top-level comment** (free-floating or git-anchored) is a **sub-thread root**.
- Authenticated users with read access (and not blocked) may post **one level of replies** on any visible root comment, using the same Markdown safety posture as existing comments.
- Replies may optionally attach a **git anchor** (ref, commit SHA, file path, line/range) pointing at code different from the root anchor, using the same attach flow as top-level anchored comments.
- The **root comment author** or a **Writer+** member may **resolve** or **unresolve** a sub-thread. Resolution is **visual only**: a resolved badge and **collapsed** presentation; replies remain permitted and do not automatically clear the badge.
- Posting a **new reply** on a resolved, collapsed sub-thread **auto-expands** it so the new content is visible immediately; the resolved badge remains until someone with permission explicitly unresolves.
- Top-level comments remain in **strict chronological order** by root `createdAt`; resolved sub-threads are **collapsed in place** (not moved to a separate section).
- **Discussion-level** status (Open, Engaged, Resolved, Dismissed) is unchanged and independent: resolving a sub-thread does not resolve the discussion.
- **Lifecycle hooks** differentiate top-level comments from replies: only top-level comments may trigger **Engaged**; replies on a closed discussion still **reopen** it to Open (never Engaged again), matching existing reopen semantics.
- **Notifications**: new replies notify all discussion subscribers; sub-thread resolved notifies the root author and discussion assignee only (not all subscribers). Unresolve does not notify.
- When a root comment is **soft-deleted** but has replies, the root is hidden and replies are shown as **pseudo-top-level** entries labeled as replying to a deleted comment; that branch becomes **read-only** (no further replies).

## User Stories

### Sub-thread structure

1. As a signed-in user with read access (and not blocked), I want to **reply** to a top-level comment on a discussion, so that I can continue a conversation on that specific note without starting a new top-level entry.
2. As a participant, I want replies to appear **nested under** their root comment, so that context stays grouped.
3. As a participant, I want **only one level** of nesting (reply to root, not reply to reply), so that the thread model stays simple and scannable.
4. As a repository visitor, I want the comment list API to return top-level comments each with a `replies` array, so that the client can render sub-threads in one request.
5. As a participant, I want top-level comments to remain sorted **chronologically by creation time**, so that the overall discussion timeline is predictable.

### Replies with code context

6. As a reply author, I want to write reply bodies in **Markdown**, so that I can add formatted notes and fenced code snippets.
7. As a reply author, I want to optionally **attach a git anchor** on a reply (file, line, ref), so that I can point at different code than the root comment without creating a new top-level thread.
8. As a repository visitor, I want anchored replies to show the same anchor preview and relocation behavior as top-level anchored comments, so that code context is consistent.
9. As a reply author, I want anchor attachment to use the same repository browse / line-pick flow as top-level comments, so that the UX is familiar.

### Sub-thread resolution

10. As the **author of a root comment**, I want to mark my sub-thread as **resolved**, so that I can signal the note has been addressed without closing the whole discussion.
11. As a **Writer+** member, I want to resolve any sub-thread, so that maintainers can triage code-review notes on behalf of authors.
12. As the root author or a Writer+ member, I want to **unresolve** a sub-thread, so that I can reopen a branch that was resolved prematurely.
13. As a repository visitor, I want resolved sub-threads to show a clear **resolved badge** on the root, so that I can see which notes are addressed.
14. As a repository visitor, I want resolved sub-threads to be **collapsed by default**, so that I can quickly see where discussion is still active.
15. As a repository visitor, I want to **expand** a collapsed resolved sub-thread manually, so that I can read history when needed.
16. As a participant, I want to **still post replies** on a resolved sub-thread, so that follow-up conversation is not blocked by resolution.
17. As a participant posting a reply on a resolved, collapsed sub-thread, I want the sub-thread to **auto-expand**, so that my new reply is immediately visible.
18. As a participant, I want the resolved badge to **remain** after a new reply until someone explicitly unresolves, so that "resolved" means "was addressed" unless cleared manually.
19. As a Reader without Writer role, I want to be **unable** to resolve or unresolve sub-threads I did not author, so that triage stays with authors and maintainers.
20. As a participant, I want sub-thread resolution to **not change** the parent discussion's Open/Engaged/Resolved/Dismissed status, so that per-note triage is independent of whole-discussion closure.

### Discussion lifecycle interaction

21. As a non-creator participant, I want my **first top-level comment** (not a reply) to still trigger **Engaged** on an Open discussion once per lifetime, so that existing engagement semantics are preserved.
22. As a participant, I want **replies** never to trigger the Engaged transition, so that follow-ups on existing notes are not mistaken for new engagement.
23. As a participant, I want a **reply** on a Resolved or Dismissed discussion to **reopen** it to Open, so that activity on any branch can revive a closed discussion.
24. As a participant reopening via reply, I want the discussion to return to **Open** (never Engaged again), so that reopen semantics match the parent PRD.

### Notifications

25. As a discussion subscriber, I want to be notified when someone posts a **reply** on any sub-thread, so that I stay aware of follow-up conversation.
26. As the **root comment author**, I want to be notified when my sub-thread is marked resolved, so that I know a maintainer addressed my note.
27. As the **discussion assignee**, I want to be notified when any sub-thread is marked resolved, so that I can track triage progress.
28. As a discussion subscriber, I want **not** to receive a notification on every sub-thread resolve, so that notification volume stays manageable.
29. As any user, I want **no notification** on sub-thread unresolve, so that undoing a mistaken resolve is quiet.

### Soft-delete and moderation

30. As a reply author, I want to **edit** and **soft-delete** my own replies with the same rules as top-level comments, so that moderation is consistent.
31. As a Writer+ member, I want to soft-delete any reply for moderation, so that abusive follow-ups can be removed.
32. As a repository visitor, I want soft-deleted replies hidden from the default thread view, so that moderated content does not appear.
33. As a root comment author, I want to soft-delete my root comment, so that I can retract an opening note.
34. As a repository visitor, when a root comment with replies is soft-deleted, I want the root hidden and **replies still visible** with an indicator that they reply to a **deleted comment**, so that conversation history is not lost.
35. As a participant, I want a sub-thread whose root was soft-deleted to be **read-only** (no new replies), so that orphan branches do not accumulate ambiguous conversation.
36. As an operator, I want soft-deleted comments **never hard-deleted** at the persistence layer, so that auditability is preserved (consistent with parent PRD).

### Mentions and subscriptions

37. As a reply author, I want to @mention users in reply bodies, so that I can draw attention to follow-ups (same rules as top-level comments).
38. As a commenter posting a reply, I want to be **auto-subscribed** to the discussion if not already, so that I receive updates on threads I join.

### Authorization and access

39. As a blocked user, I want to be prevented from posting replies, so that participation controls apply uniformly.
40. As an anonymous visitor on a public repository, I want to **read** sub-threads but not reply without signing in, so that read access matches the parent PRD.
41. As a Reader on a private repository without access, I want sub-threads inaccessible with the same 403/404 semantics as discussions, so that permissions stay consistent.

### Web UI

42. As a repository visitor on the discussion detail page, I want each top-level comment to show a **Reply** action and inline reply composer, so that follow-ups are low friction.
43. As a root author or Writer+, I want **Resolve** / **Unresolve** controls on the sub-thread header, so that triage is one click.
44. As a repository visitor, I want resolved sub-threads visually distinct and collapsed, with unresolved threads expanded, so that active work stands out in a chronologically ordered list.
45. As a repository visitor, I want deep links (URL hash) to individual comments to still work for roots and replies, so that sharing specific notes remains possible.

### API and integration

46. As a developer, I want sub-thread APIs documented in OpenAPI, so that the web client stays in sync.
47. As a developer integrating anchored replies, I want reply creation to accept the same anchor payload shape as top-level comment creation, so that clients reuse existing types.

## Implementation Decisions

### Assumptions

- **Depends on** thread comments (disc-04) and anchored comments (disc-09) from the parent discussions work being available.
- **Extends** the existing `DiscussionComment` aggregate rather than introducing a separate entity type for replies.
- Discussion-level resolve/dismiss (Writer+, whole discussion) remains as implemented; this PRD adds **sub-thread resolution** only on root comments.
- Comment pagination remains deferred; full thread load (roots + nested replies) is acceptable for v1.
- Real-time updates (WebSockets) remain out of scope; refresh or polling acceptable.

### Major modules

The work extends three existing deep modules from the parent PRD and adds one focused module. Each keeps a narrow interface.

#### 1. Comment Threading (extends Comment & Markdown)

**Interface:** create comment (with optional `parentCommentId`), list comments for discussion (roots with nested `replies`).

**Responsibilities:**

- Add optional `parentCommentId` on comments. `null` = top-level (sub-thread root); non-null = reply.
- Enforce **one level of nesting**: `parentCommentId` may only reference a top-level comment (the parent must have `parentCommentId == null`). Replies to replies are rejected (400).
- Reject reply creation when the parent root is soft-deleted (read-only orphan branch).
- Reject reply creation when the parent is itself a reply.
- Replies share the same Markdown pipeline, edit, and soft-delete rules as top-level comments.
- Replies may carry an optional git anchor (same payload and persistence as top-level anchored comments).
- List response shape: ordered top-level comments by `createdAt` ascending; each includes `replies[]` ordered by `createdAt` ascending.
- Projection fields on root DTOs useful for UI: `replyCount`, `lastReplyAt`, `resolvedAt`, `resolvedByUserId`, `isResolved` (derived).

Does **not** own sub-thread resolve logic or notification fan-out rules.

#### 2. Sub-Thread Resolution (new)

**Interface:** `ResolveSubThread(commentId)`, `UnresolveSubThread(commentId)`.

**Responsibilities:**

- Only **root comments** (`parentCommentId == null`) may be resolved or unresolved.
- Authorization: root comment **author** or **Writer+** on the repository.
- Persist `resolvedAt` and `resolvedByUserId` on the root comment row. Unresolve clears both fields.
- Resolution is **idempotent** (resolve already-resolved is no-op or 200; same for unresolve).
- Resolution does **not** modify discussion `status`, `hasEverBeenEngaged`, or `updatedAt` unless a separate rule says otherwise (default: bump discussion `updatedAt` on resolve/unresolve so the discussion list reflects recent triage activity — recommended).
- Emit notification event on resolve only (see Notifications module).

Does **not** block reply creation, collapse UI, or anchor resolution.

#### 3. Discussion Lifecycle Hooks (extends Discussion Core integration)

**Interface:** unchanged public discussion APIs; modified comment-create side effects.

**Responsibilities:**

- On comment create, branch on whether the new comment is top-level or a reply:

```
Top-level comment on closed discussion → reopen to Open (existing)
Top-level comment by non-creator on Open → Engaged once (existing)
Reply on closed discussion → reopen to Open; never Engaged
Reply on Open/Engaged → no status change; never Engaged
```

- Reopen-via-reply uses the same `Reopened` notification event as top-level reopen.

Does **not** interpret sub-thread resolved state.

#### 4. Notifications (extends Notifications)

**Interface:** `Notify(event)` with new event type.

**Responsibilities:**

- Add event type: **SubThreadResolved** (name TBD; distinct from discussion-level `Resolved`).
- **New reply**: treat as `NewComment` for all discussion subscribers (existing behavior; reply is still a comment on the discussion).
- **Sub-thread resolved**: notify **root comment author** and **discussion assignee** (if set). Do not fan out to all subscribers.
- **Sub-thread unresolve**: no notification.
- Email (if parent email notifications shipped): same subject prefix per discussion; event summary distinguishes sub-thread resolved from discussion resolved.

#### 5. Orphan Reply Presentation (extends Comment soft-delete)

**Interface:** list projection behavior when root is soft-deleted.

**Responsibilities:**

- When listing comments, roots with `deletedAt != null` are excluded from the root list.
- Replies whose parent root is soft-deleted are **promoted to the root list** for API/UI purposes with metadata: `orphanedFromDeletedRoot: true` (or equivalent).
- Promoted orphans are visually labeled "in reply to deleted comment"; `parentCommentId` retained in data for audit but reply creation on that branch is blocked.
- Soft-deleting a root does **not** soft-delete its replies.

#### 6. Web Client (discussion detail)

**Interface:** render nested sub-threads; reply composer; resolve toggle; collapse resolved roots.

**Responsibilities:**

- Render top-level comments chronologically; nest `replies` under each root.
- Root header: author, timestamp, anchor preview (if any), resolve/unresolve (if permitted), reply toggle.
- Resolved roots: collapsed by default; show resolved badge and reply count / last activity on collapsed header.
- New reply on collapsed resolved root: expand thread; do not clear resolved badge.
- Inline reply composer per root (not the bottom discussion composer, which remains for **new top-level comments** only).
- Reuse existing code-attach modal for optional anchors on replies.
- Orphan promoted replies: distinct muted label; no reply button.

### Schema changes (conceptual)

On `discussion_comments` (or equivalent):

| Field | Applies to | Notes |
|-------|------------|-------|
| `parent_comment_id` | all | nullable FK to same table; null = root |
| `resolved_at` | roots only | nullable timestamp |
| `resolved_by_user_id` | roots only | nullable FK to user |

Constraints:

- Check or application rule: `resolved_at` non-null only when `parent_comment_id` is null.
- Index on `(discussion_id, parent_comment_id, created_at)` for list queries.

### API surface (conceptual)

Extends parent discussion comment APIs:

- `GET /{owner}/{repo}/discussions/{number}/comments` — returns roots with nested `replies[]`; includes orphan-promoted entries when parent root deleted.
- `POST /{owner}/{repo}/discussions/{number}/comments` — body adds optional `parentCommentId`; optional `anchor` unchanged.
- `POST /{owner}/{repo}/discussions/{number}/comments/{commentId}/resolve` — root author or Writer+.
- `POST /{owner}/{repo}/discussions/{number}/comments/{commentId}/unresolve` — root author or Writer+.

Existing edit and soft-delete endpoints apply to replies unchanged.

### Authorization matrix (additions)

| Action | Reader+ (not blocked) | Writer+ |
|--------|----------------------|---------|
| Reply to root comment | Yes (author or any reader) | Yes |
| Reply to orphan branch (deleted root) | No | No |
| Resolve / unresolve own root sub-thread | Yes (if root author) | Yes |
| Resolve / unresolve any sub-thread | No | Yes |

### State: sub-thread resolution (visual)

```
[*] → Unresolved (default)

Unresolved → Resolved   by root author or Writer+
Resolved → Unresolved     by root author or Writer+ (explicit unresolve)

Resolved + new reply → still Resolved (badge remains); UI expands
```

Sub-thread resolution does not interact with discussion status state machine.

## Testing Decisions

### Principles

- Test **observable behavior** through HTTP APIs and authorization outcomes, not internal nesting implementation.
- Prefer table-driven tests for role matrices (root author, other Reader, Writer+, blocked user).
- Verify list response nesting shape and ordering in integration tests.
- UI collapse/expand behavior is manual or component-tested; API tests prove data for collapse decisions (`isResolved`, `replyCount`).

### Modules and prior art

| Module | Test focus | Prior art |
|--------|------------|-----------|
| Comment Threading | Create reply under root; reject reply-to-reply; reject reply on deleted root; nested list shape and order; anchored reply stores anchor | `ListDiscussionCommentsQueryHandlerTests`, `CreateDiscussionCommentQueryHandler` patterns |
| Sub-Thread Resolution | Resolve/unresolve permissions; only roots resolvable; idempotent resolve; discussion status unchanged | `DiscussionLifecycleQueryHandlers` resolve/dismiss tests |
| Lifecycle hooks | Reply on closed discussion reopens; reply never Engages; top-level still Engages once | `CreateDiscussionCommentQueryHandler` engagement tests |
| Notifications | Reply triggers NewComment to subscribers; resolve notifies root author + assignee only; unresolve silent | Notification handler tests from disc-07 |
| Orphan replies | Soft-delete root hides root; replies promoted with orphan flag; reply blocked | Soft-delete comment handler tests |

### Integration scenarios

- Create root + two replies; GET returns single root with `replies.length === 2` in chronological order.
- Attempt reply with `parentCommentId` pointing at a reply → 400.
- Reader who is root author resolves own sub-thread; other Reader cannot resolve stranger's root.
- Writer+ resolves any sub-thread.
- Resolve sub-thread; discussion status unchanged; POST reply still succeeds; discussion still not auto-unresolved.
- Reply on Resolved discussion → discussion Open; `hasEverBeenEngaged` prevents re-Engage.
- Top-level comment on Open discussion by non-creator → Engaged; subsequent reply by another non-creator does not Engage.
- Soft-delete root with replies; list shows promoted orphans; POST reply to orphan parent id → 403/400.
- Anchored reply persists anchor; anchor resolver runs on read.

### Out of scope for automated tests in v1

- Visual regression of collapse/expand animations.
- Email client rendering of sub-thread resolved vs discussion resolved subjects.
- Performance with very large reply counts per root.

## Out of Scope

- **More than one level of nesting** (reply to reply).
- **Sub-thread resolve affecting discussion-level status** (resolve/dismiss whole discussion remains separate).
- **Auto-unresolve on new reply** (badge stays until explicit unresolve).
- **Re-sorting** roots (unresolved-first sections, activity-based resort); strict chronological only.
- **Locking replies** on resolved sub-threads (resolution is visual-only; replies always allowed except orphan read-only branches).
- **Notifications on unresolve**.
- **Notifications on sub-thread resolve to all subscribers** (only root author + assignee).
- **Data re-parenting** when root is soft-deleted (visual promotion only).
- **Separate "reopen sub-thread" action** distinct from unresolve.
- **Per-reply resolution** (only roots are resolvable).
- **Hard delete** of comments.
- **Real-time** push of new replies or resolve state.
- **Comment pagination** (deferred; same as parent PRD).
- **Cross-discussion** sub-threads.

## Further Notes

### Relationship to parent PRD

The parent [repository-discussions PRD](./repository-discussions.md) explicitly listed **comment threading (nested replies)** as out of scope for v1 with a flat chronological list. This PRD is the v2 extension for **one-level sub-threads** and **per-root resolution**. When both ship, update the parent PRD out-of-scope bullet to reference this document instead of deferring indefinitely.

### Suggested implementation order (tracer bullets)

1. **ST-01 — Schema and threading:** `parentCommentId`, create reply validation, nested list projection.
2. **ST-02 — Lifecycle differentiation:** top-level vs reply hooks for Engaged and reopen.
3. **ST-03 — Sub-thread resolution:** resolve/unresolve API, persistence, authorization.
4. **ST-04 — Orphan replies:** soft-delete root presentation and reply-block rules.
5. **ST-05 — Notifications:** SubThreadResolved event and scoped fan-out.
6. **ST-06 — Web UI:** nested render, collapse, reply composer, resolve toggle, anchored replies.
7. **ST-07 — Integration tests** across threading, resolve, lifecycle, orphan, and notification scenarios.

### Suggested issue ID

`disc-11` in `docs/issues/repository-discussions/`, blocked by disc-04 and disc-09.

### Naming reference

- **Sub-thread** — one root comment and its replies (zero or more).
- **Root comment** — top-level comment (`parentCommentId` null); only roots may be resolved.
- **Reply** — comment with `parentCommentId` set to a root.
- **Orphan reply** — reply whose root was soft-deleted; promoted in list with read-only branch.
- **Discussion-level resolved** — existing four-state discussion lifecycle; unchanged by this PRD.
