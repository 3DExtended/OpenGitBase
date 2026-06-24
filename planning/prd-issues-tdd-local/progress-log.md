# PRD issues TDD — progress log

## 2026-06-24

### disc-11 — Basic sub-thread replies
- Added `parentCommentId`, nested list projection, reply lifecycle split (no Engage on replies; reopen on closed discussion).
- Handler tests: create reply, reject reply-to-reply, reject reply to deleted root.
- **Status:** completed on `main`

### disc-12 — Anchored replies
- Reused anchor payload on reply create; `DiscussionSubThread` reply composer with code-attach modal.
- **Status:** completed on `main`

### disc-13 — Sub-thread resolve and collapse UI
- Added `resolvedAt` / `resolvedByUserId`, resolve/unresolve API, `DiscussionSubThread` collapse + badge UI.
- **Status:** completed on `main`

### disc-14 — Orphan replies
- `BuildNestedCommentList` promotes orphans when root soft-deleted; blocks new replies.
- **Status:** completed on `main`

### disc-15 — Sub-thread resolve notifications
- `NotificationEventType.SubThreadResolved`, `RestrictToExplicitRecipients` on notification query.
- **Status:** completed on `main`

### disc-16 — Integration tests
- Extended `scripts/test-discussions-e2e.sh` with nested reply + resolve smoke.
- Playwright golden snapshots for `DiscussionSubThread` visual gallery.
- **Status:** completed on `main`
