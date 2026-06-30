# Capturing SendGrid sender + E2E mail API

## Metadata

- ID: e2e-07
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-regression-framework.md

## Parent

[PRD: Unified E2E Regression Framework](../../prd/e2e-regression-framework.md)

## What to build

Replace real SendGrid in E2E runs with an in-memory capture pipeline:

1. **CapturingSendGridEmailSender** — implements existing `ISendGridEmailSender`; stores full message including HTML body.
2. **E2E configuration flag** — enabled via compose/runner environment (aligned with existing `Debug__Features__*` patterns).
3. **E2E-only query endpoint** — list/clear captured messages filtered by recipient; disabled outside E2E/Development defaults.
4. **Test helper** — fetch captured mail from tests; auto-record `EmailCaptured` wire events in transcript.
5. **Side-channel baseline** — `side-channel/emails/{step-id}.json` with normalized subject/body (verification tokens as placeholders).

Vertical test: trigger an email-sending API path (e.g. resend verification or password reset) → assert capture → baseline includes normalized email artifact.

## Acceptance criteria

- [ ] No real SendGrid calls when E2E capture flag enabled
- [ ] Captured emails include full HTML body retrievable by tests
- [ ] E2E mail endpoint unreachable with production-default configuration
- [ ] Transcript records email capture events
- [ ] Side-channel email baseline normalizes verification tokens/links
- [ ] Unit tests for capturing sender; integration test against EmailSend pipeline

## Blocked by

- [06-test-isolation-db-reset-normalization.md](./06-test-isolation-db-reset-normalization.md)

## User stories covered

- 22, 47, 48, 49, 50

## Notes

- Reuses `EmailSendQueryHandler` → `ISendGridEmailSender` path; no Mailpit container.
- Auth journey (e2e-08) parses verification content from captured HTML.
