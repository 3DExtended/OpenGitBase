<!-- forge: #24 -->

# PRD: Repository Discussions (Threads, Code Comments, Notifications)

## Problem Statement

OpenGitBase provides repository hosting, membership roles, git access, and (in progress) web code browsing, but users cannot collaborate around code or repository problems inside the product. There is no place to open a tracked conversation about a bug, design question, or line-specific note; no way to assign work, tag discussions, or get notified when someone responds.

Developers expect a forge-style experience where they can discuss a repository without leaving the browser — including comments anchored to specific lines in the source tree that remain tied to the conversation even when files move, rename, or change. Operators and maintainers also need lightweight moderation: block disruptive users from participating without revoking their read access, and close discussions that are resolved or not actionable.

Today, repository web browsing explicitly defers issues and pull requests. This PRD defines the **Discussion** feature as the first collaboration layer: a unified container for free-floating thread comments and git-anchored code comments, with lifecycle, tags, assignee, mentions, and notifications. Linked change proposals and merge-request-style approvals are out of scope for this PRD and will build on these foundations later.

## Solution

Add **repository discussions**: per-repository, sequentially numbered threads (`/{owner}/{repo}/discussions/42`) that any user with read access can view. Authenticated users with read access can create discussions and comment; anonymous users can read discussions on **public** repositories only.

Each discussion has a required title, optional opening body, repo-defined tags, creator, optional single assignee, and a four-state lifecycle:

- **Open** — newly created or reopened.
- **Engaged** — entered automatically exactly once per discussion lifetime when the first non-creator posts a comment.
- **Resolved** — closed positively (fixed, answered, addressed).
- **Dismissed** — closed without action (won’t fix, invalid, duplicate, etc.).

Reopening is **comment-only**: there is no silent “Reopen” control. Posting a comment on a Resolved or Dismissed discussion reopens it to **Open**. The Engaged transition does not fire again after the first engagement, including after reopen.

Comments support **Markdown** with the same safety posture as repository README rendering (no raw HTML in source; sanitized output). Users can @mention other users who have read access to the repository; mentioned users are auto-subscribed. **Anchored comments** attach to the ref currently being browsed plus `commitSha`, `filePath`, and `line` (or range) at comment time; a git-aware resolver attempts to relocate anchors on display and marks them outdated when relocation fails.

**Writer+** repository members can resolve, dismiss, and moderate comments. **Admin** and **Owner** can block users from creating or commenting (read-only mute) via repository settings.

Notifications are delivered **in-app** and by **immediate email** (SendGrid) to subscribed users, with a **stable email subject prefix per discussion** so mail clients group messages by thread.

## User Stories

### Discovery and access

1. As an anonymous visitor, I want to read discussions on a **public** repository, so that I can follow project conversation without an account.
2. As an anonymous visitor on a public repository, I want to be required to sign in before creating a discussion or commenting, so that participation is attributable.
3. As a signed-in user with read access to a repository, I want to see a list of all discussions for that repository, so that I can find ongoing and past conversations.
4. As a signed-in user without read access to a private repository, I want discussions to be inaccessible with the same semantics as private code browse (**404** for anonymous, **403** for authenticated outsiders), so that permissions stay consistent.
5. As a repository visitor, I want the discussion list sorted by **recently updated** by default, so that active threads surface first.
6. As a repository visitor, I want to filter discussions by status, tag, and assignee, so that I can triage what matters to me.
7. As a repository visitor, I want shareable URLs using a per-repository sequential number (`/discussions/42`), so that links are human-friendly and stable when titles change.

### Creating and viewing discussions

8. As a signed-in user with read access, I want to create a new discussion with a **required title** and optional body, so that threads have a clear subject.
9. As a signed-in user creating a discussion from a code anchor, I want the title **auto-suggested** from file path and line (e.g. “Note on `src/foo.ts:42`”) with the ability to edit before submit, so that quick notes are low friction.
10. As a discussion creator, I want to assign repo-defined tags at creation time, so that the thread is categorized immediately.
11. As a discussion creator, I want to optionally set an assignee who is a repository member, so that ownership is visible.
12. As a repository visitor with read access, I want to view a discussion’s title, status, tags, creator, assignee, timestamps, and full comment thread, so that I have full context.
13. As a repository visitor, I want Open and Engaged discussions visually distinguished from Resolved and Dismissed, so that I can see what is still active at a glance.

### Discussion lifecycle

14. As a discussion creator, I want my new discussion to start in **Open** status, so that the default state is actionable.
15. As a non-creator participant, I want the discussion to move to **Engaged** automatically when I post the **first** comment from someone other than the creator, so that the creator knows someone has responded.
16. As a participant on a discussion that was already Engaged once, I want subsequent non-creator comments **not** to re-trigger Engaged after a reopen, so that “Engaged” means first engagement only.
17. As a Writer+ repository member, I want to **resolve** a discussion, so that I can mark it positively closed.
18. As a Writer+ repository member, I want to **dismiss** a discussion from Open or Engaged, so that I can close untriaged or non-actionable threads.
19. As a Writer+ repository member, I want to dismiss a discussion directly from **Open** without waiting for engagement, so that spam or invalid reports can be closed immediately.
20. As a participant, I want to reopen a Resolved or Dismissed discussion **only by posting a comment**, so that reopening always carries context.
21. As a participant reopening a discussion, I want it to return to **Open** (never Engaged), regardless of who comments, so that reopen semantics are predictable.
22. As a discussion creator, I want to edit the title and tags while the discussion is Open or Engaged, so that I can fix mistakes.
23. As a Writer+ member, I want to edit any discussion’s title, tags, and assignee for triage, so that maintainers can organize work.
24. As any user, I want title and tags on Resolved/Dismissed discussions to be locked until reopen via comment, so that closed threads stay stable.

### Thread comments

25. As a signed-in user with read access (and not blocked), I want to post free-floating comments on a discussion, so that I can converse without pointing at code.
26. As a comment author, I want to write comments in **Markdown**, so that I can format code snippets and emphasis.
27. As a security reviewer, I want rendered comment HTML sanitized and raw HTML disallowed in source, so that discussions cannot be used for XSS.
28. As a comment author, I want to edit my own comments at any time with an “edited” indicator, so that I can correct mistakes.
29. As a comment author, I want to soft-delete my own comments, so that I can retract content while preserving auditability.
30. As a Writer+ member, I want to soft-delete any comment for moderation, so that abusive content can be removed from view.
31. As an operator, I want the database to **never hard-delete** comments, so that moderated or deleted content remains auditable.
32. As a participant, I want commenting on a closed discussion to reopen it to Open, so that conversation can continue without a separate reopen action.

### Anchored (code) comments

33. As a signed-in user browsing a file at a specific ref, I want to add a comment anchored to a line (or range), so that feedback is tied to exact code context.
34. As a comment author, I want the anchor to record the **browsed ref**, **commit SHA at comment time**, **file path**, and **line**, so that the comment has a stable snapshot.
35. As a repository visitor viewing an anchored comment later, I want the UI to attempt to **relocate** the anchor on the current ref tip, so that I can jump to the relevant code when it still exists.
36. As a repository visitor, I want outdated anchors to show clearly when the line moved, the file was deleted, or relocation fails, so that I know context may have drifted.
37. As a repository visitor, I want the parent discussion to remain intact even when anchors are orphaned, so that conversation history is never lost.
38. As a participant, I want anchored comments to participate in the same discussion lifecycle and permissions as thread comments, so that there is one collaboration model.

### Tags

39. As a Writer+ member, I want to create, rename, and delete repository-scoped tags, so that the team shares a consistent label vocabulary.
40. As an Admin or Owner, I want to manage the tag catalog in repository settings, so that tag hygiene is a maintainer concern.
41. As a Reader+ member, I want to apply existing tags when creating or editing a discussion I can access, so that I can categorize without managing the catalog.
42. As a repository visitor, I want to filter the discussion list by tag, so that I can focus on areas like `bug` or `question`.

### Assignee

43. As a discussion creator or Writer+ member, I want to set or change a single optional assignee from repository members, so that responsibility is explicit.
44. As an assignee, I want to be auto-subscribed to notifications for that discussion, so that I do not miss updates.

### Mentions

45. As a comment author, I want to @mention users who have read access to the repository, so that I can draw someone’s attention.
46. As a mentioned user, I want to be auto-subscribed to the discussion, so that I receive updates.
47. As a platform, I want to reject or ignore mentions of users outside the repository read audience, so that mentions respect visibility boundaries.

### Subscriptions and notifications

48. As a discussion creator, I want to be auto-subscribed to my discussion, so that I am notified of responses.
49. As a commenter, I want to be auto-subscribed when I comment, so that I follow the thread I joined.
50. As a subscriber, I want to unsubscribe from a discussion, so that I can reduce noise.
51. As a subscriber, I want an **in-app notification inbox** (bell icon, unread count, list), so that I see activity when logged in.
52. As a subscriber, I want **immediate email** notifications for subscribed events, so that I am alerted when away from the app.
53. As an email recipient, I want a **stable subject line prefix** per discussion (e.g. `[org/repo #42] …`), so that my mail client groups messages by thread.
54. As a subscriber, I want notifications for new comments, @mentions, assignee changes, and resolve/dismiss events, so that I stay informed of meaningful changes.
55. As a user who is not subscribed, I want not to receive notifications, so that only interested parties are notified.

### Participation controls (blocking)

56. As an Admin or Owner, I want to block a user from creating discussions and commenting in my repository, so that I can mute disruptive participants.
57. As an Admin or Owner, I want a repository settings screen listing blocked users with **unblock** actions, so that moderation is reversible.
58. As a blocked user with Reader access, I want to still **read** discussions, so that block is a participation mute rather than a full ban.
59. As a blocked user, I want create and comment attempts to fail with a clear error, so that I understand why I cannot participate.

### Moderation and roles

60. As a Reader+ member who is not blocked, I want to create discussions and comment, so that read access implies participation for collaborators.
61. As a Writer+ member, I want to resolve and dismiss discussions, so that maintainers can close threads.
62. As a Writer+ member, I want to remove (soft-delete) any comment, so that moderation does not depend on the author.
63. As a security reviewer, I want discussion authorization to align with repository membership and organization read semantics used for git and web content access, so that permissions are consistent across surfaces.

### Integration with web browsing

64. As a user viewing a blob page, I want to start or add to a discussion from a selected line, so that code review notes happen in context.
65. As a user on the repository home, I want a **Discussions** tab or nav entry listing threads, so that collaboration is discoverable next to code.
66. As a developer, I want discussion APIs documented in OpenAPI, so that the web client can stay in sync.

## Implementation Decisions

### Major modules

The work splits into six deep modules with narrow interfaces. Each encapsulates substantial behavior behind a surface that should change rarely.

#### 1. Discussion Core

**Interface:** create, read, update metadata, list/filter, transition status, assign assignee, allocate per-repo sequence number.

**Responsibilities:**
- Discussion aggregate: `repositoryId`, sequential `number`, `title`, optional `body`, `status`, `creatorUserId`, optional `assigneeUserId`, timestamps.
- Status state machine:

```
[*] → Open (on create)

Open → Engaged     when first comment by non-creator (once per discussion lifetime)
Open → Dismissed   by Writer+
Engaged → Resolved by Writer+
Engaged → Dismissed by Writer+

Resolved|Dismissed → Open  when any comment is posted (reopen); never → Engaged again
```

- Track `hasEverBeenEngaged` (or equivalent) so the Engaged auto-transition fires at most once.
- Reopen is a side effect of comment creation on closed discussions, not a standalone status API.
- Per-repository monotonic sequence for public `number`; internal surrogate key for joins.
- List default sort: `updatedAt` descending; filter by status, tag, assignee.
- Authorization: mirror repository read rules for visibility; write operations require authenticated user with read access and not blocked.

Does **not** render Markdown, parse mentions, send notifications, or resolve git anchors.

#### 2. Comment & Markdown

**Interface:** add comment, edit comment, soft-delete comment, list comments for discussion (chronological, with pagination deferred).

**Responsibilities:**
- Comment aggregate: `discussionId`, `authorUserId`, `body` (Markdown source), rendered `bodyHtml`, `createdAt`, `updatedAt`, `editedAt`, soft-delete fields (`deletedAt`, `deletedByUserId`, `deletionReason` optional).
- **Hard delete prohibited** at persistence layer — all removals are soft-delete.
- Markdown pipeline aligned with repository README safety (sanitize output; disallow raw HTML in source).
- On comment create: invoke Discussion Core hooks for Engaged transition and reopen-from-closed.
- Writer+ may soft-delete any comment; author may soft-delete own comment.
- Deleted comments hidden from default UI but retained for audit/admin views (v1 may hide only; audit UI optional).

Does **not** own subscription or email delivery.

#### 3. Repository Tags

**Interface:** manage tag catalog per repository; assign/unassign tags on discussions.

**Responsibilities:**
- Tag catalog: `repositoryId`, `name`, optional `color`, `createdAt`.
- Writer+ CRUD on tags; Admin/Owner management entry in repo settings.
- Reader+ may assign existing tags on discussions they can edit (creator on own Open/Engaged; Writer+ on any Open/Engaged).
- Many-to-many discussion↔tag assignments.

#### 4. Participation Controls

**Interface:** `IsBlocked(repositoryId, userId)`, `Block`, `Unblock`, list blocked users.

**Responsibilities:**
- `repository_blocked_users` (or equivalent): `repositoryId`, `userId`, `blockedByUserId`, `blockedAt`, optional `reason`.
- Only Admin+Owner may block/unblock.
- Enforcement at discussion create and comment create (and mention/assign if applicable).
- Blocked users retain read access if they still have Reader+ membership.

#### 5. Code Anchoring

**Interface:** attach anchor payload to a comment; `ResolveAnchor(repository, ref, anchor) → Located | Outdated | Orphaned`.

**Responsibilities:**
- Anchor payload on anchored comments: `ref`, `commitSha`, `filePath`, `line` (start), optional `endLine`.
- Default ref at creation: ref currently being browsed in web UI.
- Resolver uses git history (commit graph, path following, line mapping) via existing storage content / git APIs.
- Display layer shows jump-to-line when located, fallback message when outdated/orphaned.
- Anchored and thread comments share the same comment table with optional anchor fields or sidecar row.

Depends on repository web browsing (blob view, ref picker). Ship after thread comments.

#### 6. Notifications

**Interface:** `Notify(event)`, `Subscribe`, `Unsubscribe`, list in-app notifications for user.

**Responsibilities:**
- Auto-subscribe: creator, assignee (on set), commenters, @mentioned users.
- Explicit unsubscribe allowed for any subscriber.
- In-app notification records: type, discussion reference, actor, read/unread, createdAt.
- Email via SendGrid: immediate per event (no digest in v1).
- Email subject pattern: stable prefix per discussion, e.g. `[{owner}/{repo} #{number}] {event summary}` so clients thread by subject.
- Events: new comment, mention, assignee change, resolved, dismissed, reopened (via comment on closed).
- Mention parsing extracted from comment body after Markdown-safe processing (username or user-id token format TBD in implementation; must resolve only repo-readable users).

### Authorization matrix

| Action | Anonymous (public repo) | Reader+ | Writer+ | Admin/Owner |
|--------|-------------------------|---------|---------|-------------|
| View discussions | Yes | Yes | Yes | Yes |
| Create discussion | No (sign in) | Yes, if not blocked | Yes | Yes |
| Comment | No | Yes, if not blocked | Yes | Yes |
| Edit own comment | No | Yes | Yes | Yes |
| Soft-delete any comment | No | No | Yes | Yes |
| Resolve / Dismiss | No | No | Yes | Yes |
| Edit any discussion metadata | No | No | Yes | Yes |
| Edit own title/tags (Open/Engaged) | No | Yes (creator) | Yes | Yes |
| Manage tag catalog | No | No | Yes | Yes |
| Block / unblock users | No | No | No | Yes |

Private repositories: anonymous viewers receive **404** for discussion routes; authenticated users without read access receive **403** — consistent with repository content authorization.

### API surface (conceptual)

- `GET/POST /{owner}/{repo}/discussions` — list, create
- `GET/PATCH /{owner}/{repo}/discussions/{number}` — detail, update metadata
- `POST /{owner}/{repo}/discussions/{number}/resolve` — Writer+
- `POST /{owner}/{repo}/discussions/{number}/dismiss` — Writer+
- `GET/POST /{owner}/{repo}/discussions/{number}/comments` — list, create (reopen side effect)
- `PATCH/DELETE /{owner}/{repo}/discussions/{number}/comments/{id}` — edit, soft-delete
- `GET/POST/DELETE /{owner}/{repo}/tags` — tag catalog
- `GET/POST/DELETE /{owner}/{repo}/settings/blocked-users` — Admin/Owner
- `GET /notifications`, `POST /notifications/{id}/read`, subscription endpoints

Exact route shapes should follow existing API conventions and OpenAPI generation patterns.

### Schema (conceptual)

- `discussions` — core fields, status enum (`Open`, `Engaged`, `Resolved`, `Dismissed`), `has_ever_been_engaged`, `repository_id`, `number`, timestamps
- `discussion_comments` — body, soft-delete columns, optional FK to anchor extension
- `comment_anchors` — ref, commit_sha, path, line_start, line_end (optional separate table)
- `repository_tags`, `discussion_tag_assignments`
- `repository_blocked_users`
- `discussion_subscriptions`
- `notifications` — user_id, discussion_id, event_type, payload, read_at

### Web UI (conceptual)

- Repository **Discussions** list page with filters and default sort.
- Discussion detail page: header (title, status badge “Engaged” not “In discussion”), tags, assignee, comment thread, composer.
- Blob page integration: line selection → add anchored comment (creates or attaches to discussion).
- Repository settings: blocked users panel; tag management for Admin/Owner.
- Notification bell in app shell.

### Assumptions

- Session cookie authentication for web UI (consistent with repository web browsing v1).
- Organization-owned repositories use the same membership semantics as existing `RepositoryContentAuthorizationService` and git access checks.
- Engaged UI label is **Engaged**; API enum may remain `InDiscussion` or `Engaged` — pick one in implementation and map consistently.
- Comment pagination deferred; full thread load acceptable for v1 unless performance requires otherwise.
- In-app notification preferences (per-channel toggles) deferred; email sends for all subscribed events in v1.

## Testing Decisions

### Principles

- Test **observable behavior** through HTTP APIs and authorization outcomes, not internal state machine implementation details.
- Prefer table-driven tests for role matrices (Reader, Writer, Admin, Owner, anonymous, blocked).
- Integration tests should use existing Docker Compose / API test patterns where possible.

### Modules and prior art

| Module | Test focus | Prior art |
|--------|------------|-----------|
| Discussion Core | Status transitions; Engaged once-only; reopen via comment; sequential numbering per repo | `RepositoryMemberControllerTests`, CQRS handler tests |
| Comment & Markdown | Create/edit/soft-delete; reopen closed discussion; Engaged trigger on first non-creator | API controller tests, markdown sanitizer tests from browse PRD |
| Repository Tags | Writer+ catalog CRUD; Reader+ assign; filter list by tag | Feature CRUD handler patterns |
| Participation Controls | Block prevents create/comment; read still allowed; Admin/Owner only | `RepositoryMemberControllerTests` role patterns |
| Code Anchoring | Anchor stored correctly; resolver returns located/outdated; orphaned does not delete discussion | Storage content API integration tests, browse e2e |
| Notifications | Auto-subscribe rules; mention triggers; email subject format; in-app unread | SendGrid test doubles, `JWTTokenGeneratorTests`-style unit isolation |

### Integration scenarios

- Public repo: anonymous read list and detail; sign-in required for create/comment.
- Private repo: anonymous 404; outsider 403; member full access.
- Open → Engaged on first non-creator comment only.
- Reopen Resolved via comment → Open; further non-creator comments do not re-Engage.
- Writer+ dismiss from Open; Writer+ resolve from Engaged.
- Blocked Reader cannot comment; can still read.
- Anchored comment on browsed ref; display after file rename shows relocated or outdated state.
- Email notification subject contains stable `[owner/repo #n]` prefix.

### Out of scope for automated tests in v1

- Visual regression of discussion UI.
- Email deliverability and client-specific threading behavior.
- Anchor resolver accuracy across complex merge histories (smoke tests only).

## Out of Scope

- **Change proposals / merge requests** linked to discussions.
- **Approvals** on code changes that auto-resolve discussions.
- **Web push** / browser OS notifications.
- **Email digest** or batched notification summaries.
- **Reactions** (emoji), voting, pinning, duplicate linking.
- **Full-text search** across discussions.
- **Issue templates**, milestones, or project boards.
- **Cross-repository** discussions.
- **Guest / email-only** participants without platform accounts.
- **Real-time** comment updates (WebSockets); polling or refresh acceptable in v1.
- **Comment threading** (nested replies); flat chronological list in v1.
- **Hard delete** of comments or discussions.
- **PAT authentication** on discussion APIs (session auth for web v1).
- **Audit UI** for moderators to view soft-deleted comment bodies (data retained; UI optional).

## Further Notes

### Relationship to repository web browsing

Anchored comments depend on blob view, ref picker, and storage content APIs from the repository web browsing work. Thread comments and discussion core can ship before anchored comments. The repository web browsing PRD explicitly listed issues/PRs as out of scope; this PRD is the issues/discussions counterpart.

### Relationship to existing authorization

Discussion read/write gates should reuse the same effective-role semantics as `RepositoryContentAuthorizationService` and git `ReadGit` checks. Extract shared “has read access” logic rather than duplicating org/repo member rules.

### Suggested implementation order (tracer bullets)

1. **D-01 — Discussion core:** entity, list, create, status machine, assignee, sequential numbers, authorization.
2. **D-02 — Thread comments:** Markdown, edit, soft-delete, reopen-via-comment, Engaged hook.
3. **D-03 — Repository tags:** catalog + assignments + filters.
4. **D-04 — Participation controls:** blocked users API + repo settings UI (can parallelize with D-02/D-03).
5. **D-05 — Mentions + in-app notifications:** parse mentions, subscriptions, inbox.
6. **D-06 — Email notifications:** SendGrid, stable subject prefix per discussion.
7. **D-07 — Anchored comments:** anchor payload, resolver, blob page UI integration.
8. **D-08 — End-to-end integration tests** across public/private, lifecycle, block, anchor smoke.

### Naming reference

| Concept | User-facing | Notes |
|---------|-------------|-------|
| Container | Discussion | Nav: “Discussions” |
| Status `Engaged` | Engaged | Formerly “In discussion”; fires once per lifetime |
| Line comment | Code comment / anchored comment | Internal; not a separate product object in v1 |

### Deferred follow-up PRD

A subsequent PRD should cover **change proposals** (merge-request-like branches), linking commits to discussions, and **approval** flows that can automatically transition discussion status to Resolved.
