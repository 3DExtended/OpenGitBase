# Email notifications

## Metadata

- ID: disc-08
- Type: AFK
- Status: ready
- Source: docs/prd/repository-discussions.md

## Parent

[PRD: Repository Discussions (Threads, Code Comments, Notifications)](../../prd/repository-discussions.md)

## What to build

**Immediate email notifications** for discussion subscribers via existing SendGrid integration.

For each in-app notification event from disc-07, send an email to subscribed users who have a verified email (skip if no email on file).

**Subject line format (stable per discussion for mail client grouping):**

```
[{owner}/{repo} #{number}] {event summary}
```

Examples:
- `[acme/widget #42] Someone commented on your discussion`
- `[acme/widget #42] You were mentioned`
- `[acme/widget #42] Discussion resolved`

Use the same prefix for all event types on a given discussion so threads group in Gmail/Outlook.

**Body:** link to discussion detail URL; short plain-text summary; optional minimal HTML template consistent with existing transactional emails.

No digest/batching in v1 — one email per event.

## Acceptance criteria

- [ ] New comment emails sent to subscribers (excluding comment author optional — prefer notify all subscribers including author if subscribed)
- [ ] Mention email sent to mentioned user
- [ ] Assignee change email sent to new assignee
- [ ] Resolve and dismiss emails sent to subscribers
- [ ] Subject line always starts with `[{owner}/{repo} #{number}]`
- [ ] Unsubscribed user does not receive email
- [ ] SendGrid called via existing email infrastructure; failures logged, do not fail comment POST
- [ ] Unit/integration test with SendGrid test double or recorded fixture asserts subject format

## Blocked by

- [07-mentions-subscriptions-in-app-notifications.md](./07-mentions-subscriptions-in-app-notifications.md)

## User stories covered

- 52, 53

## Notes

- Per-user email opt-out preferences deferred; all subscribed users receive email in v1.
- Do not block HTTP request on email send — queue or fire-and-forget with error logging.
