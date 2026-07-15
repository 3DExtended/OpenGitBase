<!-- forge: #200 -->

# Sub-thread resolve notifications

## Metadata

- ID: disc-15
- Type: AFK
- Status: ready
- Source: docs/prd/discussion-sub-threads.md

## Parent

[PRD: Discussion Sub-Threads (Replies and Per-Comment Resolution)](../../prd/discussion-sub-threads.md)

## What to build

**Scoped notifications** for sub-thread activity: replies use existing fan-out; sub-thread resolve notifies only root author and assignee.

**New event type:** `SubThreadResolved` (distinct from discussion-level `Resolved`).

**On sub-thread resolve (disc-13):**
- Create in-app notification for **root comment author** (if not the actor).
- Create in-app notification for **discussion assignee** (if set and not the actor).
- Do **not** notify all discussion subscribers.
- Include discussion and comment references in notification payload.

**On sub-thread unresolve:**
- No notification.

**On new reply:**
- Continue existing `NewComment` behavior for all discussion subscribers (reply is a comment on the discussion).
- Auto-subscribe reply author if not already subscribed (existing disc-07 behavior).

**Email (if disc-08 shipped):**
- Immediate email for `SubThreadResolved` to same scoped recipients.
- Subject prefix unchanged per discussion; event summary distinguishes sub-thread resolved from whole-discussion resolved.

## Acceptance criteria

- [ ] `SubThreadResolved` added to notification event enum
- [ ] Resolve sub-thread notifies root author and assignee only
- [ ] Resolve does not notify unrelated subscribers
- [ ] Unresolve emits no notification
- [ ] New reply still emits `NewComment` to subscribers
- [ ] Reply author auto-subscribed per disc-07 rules
- [ ] API/handler tests for scoped fan-out on resolve
- [ ] Email test or double assertion for subject/body distinction (if disc-08 deployed)

## Blocked by

- [07-mentions-subscriptions-in-app-notifications.md](./07-mentions-subscriptions-in-app-notifications.md)
- [13-sub-thread-resolve-collapse.md](./13-sub-thread-resolve-collapse.md)

## User stories covered

- 25, 26, 27, 28, 29, 38

## Notes

- If disc-07 not yet merged, this slice can stub notification calls behind the same interface used by comment create.
- Mention parsing in reply bodies follows disc-07; no new mention rules in this slice.
