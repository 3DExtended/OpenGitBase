# Mentions, subscriptions, and in-app notifications

## Metadata

- ID: disc-07
- Type: AFK
- Status: ready
- Source: docs/prd/repository-discussions.md

## Parent

[PRD: Repository Discussions (Threads, Code Comments, Notifications)](../../prd/repository-discussions.md)

## What to build

**@mentions**, **discussion subscriptions**, and an **in-app notification inbox**.

**Mentions:**
- Parse `@username` (or agreed token) from comment Markdown on create/edit.
- Resolve only users with read access to the repository; ignore or strip invalid mentions.
- Mentioned users auto-subscribed.

**Subscriptions (`discussion_subscriptions`):**
- Auto-subscribe: discussion creator, assignee (on set/change), each commenter, each mentioned user.
- Explicit unsubscribe allowed for any subscriber.
- Creator auto-subscribed on create.

**In-app notifications (`notifications`):**
- Bell icon in app shell with unread count.
- Notification list: new comment, mention, assignee change, resolved, dismissed.
- Mark as read (per notification or mark-all).
- Only subscribed users receive notifications.

**Events to emit (in-app only in this slice; email in disc-08):**
- New comment on subscribed discussion
- User mentioned in comment
- Assignee changed
- Discussion resolved or dismissed

## Acceptance criteria

- [ ] Creator auto-subscribed on discussion create
- [ ] Commenter auto-subscribed on first comment
- [ ] @mention of repo reader creates mention notification and subscribes mentioned user
- [ ] @mention of user without repo read access ignored or rejected without error to author
- [ ] Assignee change notifies new assignee and subscribers
- [ ] Resolve/dismiss notifies subscribers
- [ ] Unsubscribe stops new notifications for that discussion
- [ ] Non-subscriber does not receive notifications
- [ ] Bell shows unread count; inbox lists notifications with discussion link
- [ ] API tests for subscribe, unsubscribe, mention parse, and notification creation

## Blocked by

- [04-thread-comments-engagement-lifecycle.md](./04-thread-comments-engagement-lifecycle.md)

## User stories covered

- 44, 45, 46, 47, 48, 49, 50, 51, 54, 55

## Notes

- Email delivery deferred to disc-08; emit domain events or call notification service interface that disc-08 extends.
- Real-time push (WebSockets) out of scope; poll or refresh on navigation in v1.
