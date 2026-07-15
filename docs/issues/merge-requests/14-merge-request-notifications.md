<!-- forge: #174 -->

# Merge request notifications

## Metadata

- ID: mr-14
- Type: AFK
- Status: ready
- Source: docs/prd/merge-requests.md

## Parent

[PRD: Merge Requests (Branch Protection, Review, and Server-Side Merge)](../../prd/merge-requests.md)

## What to build

In-app and email **notifications** for merge request events, extending the discussions notification infrastructure.

**Schema:** nullable `mergeRequestId` on notifications; new event types (approval, approval dismissed, approved, merged, closed, new comment, mention, review thread resolved).

**Subscription rules:**

- Auto-subscribe MR author, commenters, approvers
- Explicit watch toggle defer v1 optional

**Events (v1):**

- New MR comment / review reply → subscribers (`NewComment`-equivalent)
- @mention → mentioned user
- Approval given → author
- Approvals dismissed → author + prior approvers
- Open → Approved → author + subscribers
- Merged / Closed → author + subscribers
- Review thread resolved → root author (`SubThreadResolved`-equivalent)

**Email:** immediate SendGrid; subject prefix `[owner/repo!n]`

**Web:** bell inbox deep links to merge request detail

## Acceptance criteria

- [ ] Notification rows reference merge request and render in existing bell UI
- [ ] Author notified on approval and on merge
- [ ] Prior approvers notified when approvals dismissed
- [ ] Mention in MR body/comment notifies target
- [ ] Email subject contains `[owner/repo!n]` stable prefix
- [ ] Discussion Resolved notification still fires for closes-on-merge (mr-08) — no duplicate spam acceptable
- [ ] Handler tests for subscription rules and event fan-out

## Blocked by

- [06-merge-request-core-api-and-ui-shell.md](./06-merge-request-core-api-and-ui-shell.md)
- [07-mentions-subscriptions-in-app-notifications.md](../repository-discussions/07-mentions-subscriptions-in-app-notifications.md)
- [08-email-notifications.md](../repository-discussions/08-email-notifications.md)

## User stories covered

- 79, 80, 81, 82, 83, 84

## Notes

- Reuse `CreateDiscussionNotificationQuery` patterns or parallel MR query in merge-request feature.
