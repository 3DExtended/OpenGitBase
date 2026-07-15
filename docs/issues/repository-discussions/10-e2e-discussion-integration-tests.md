<!-- forge: #195 -->

# End-to-end discussion integration tests

## Metadata

- ID: disc-10
- Type: AFK
- Status: ready
- Source: docs/prd/repository-discussions.md

## Parent

[PRD: Repository Discussions (Threads, Code Comments, Notifications)](../../prd/repository-discussions.md)

## What to build

**End-to-end integration test suite** for repository discussions across API and (where feasible) scripted UI flows against the Docker Compose stack.

Cover cross-slice behavior in one verifiable path:

1. **Public repo:** anonymous read list/detail; sign-in required to create and comment.
2. **Private repo:** anonymous 404; outsider 403; member create and read.
3. **Lifecycle:** Open → Engaged on first non-creator comment; Writer+ resolve; reopen via comment → Open without re-Engage.
4. **Blocking:** blocked user reads but cannot create/comment; unblock restores participation.
5. **Notifications:** subscriber receives in-app notification on comment; email subject contains `[owner/repo #n]` prefix (mock or capture SendGrid).
6. **Tags:** filter list by tag (smoke).
7. **Anchors:** create anchored comment; resolver returns located or outdated on fixture repo (smoke).

Deliver as `scripts/test-discussions-e2e.sh` (or equivalent) plus API integration tests in `tests/` where appropriate. Document one-line run instruction in README or agent docs.

## Acceptance criteria

- [ ] Script runs against default Compose stack with documented prerequisites
- [ ] Public anonymous read passes
- [ ] Public create without auth fails
- [ ] Private anonymous 404 and outsider 403 pass
- [ ] Engaged once + reopen without re-Engage pass
- [ ] Block mute enforced; unblock restores write
- [ ] Email subject format assertion (mock/capture)
- [ ] Anchor smoke on fixture repository
- [ ] CI or developer docs mention how to run the test
- [ ] Exit non-zero on any failure

## Blocked by

- [03-discussion-detail-assignee-writer-close.md](./03-discussion-detail-assignee-writer-close.md)
- [04-thread-comments-engagement-lifecycle.md](./04-thread-comments-engagement-lifecycle.md)
- [06-blocked-users-participation-controls.md](./06-blocked-users-participation-controls.md)
- [07-mentions-subscriptions-in-app-notifications.md](./07-mentions-subscriptions-in-app-notifications.md)
- [08-email-notifications.md](./08-email-notifications.md)
- [09-anchored-code-comments.md](./09-anchored-code-comments.md)

## User stories covered

- Cross-cutting verification of stories 1–4, 15–16, 20–21, 33–36, 52–53, 56–59

## Notes

- Follow patterns from `scripts/test-ha-storage-e2e.sh` and repository browse e2e issues.
- disc-05 (tags) not a hard blocker but include tag filter smoke if merged.
- May split API-only tests from full stack script if UI automation is too heavy for v1.
