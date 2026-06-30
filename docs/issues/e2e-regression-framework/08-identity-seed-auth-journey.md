# Identity seed tier + auth journey test

## Metadata

- ID: e2e-08
- Type: AFK
- Status: ready
- Source: docs/prd/e2e-regression-framework.md

## Parent

[PRD: Unified E2E Regression Framework](../../prd/e2e-regression-framework.md)

## What to build

Populate Tier 1 with real auth fixtures and one end-to-end onboarding scenario:

**Seed path (fast):**

- Create core roles via API: admin, writer, outsider (and owner where needed).
- May use debug email verify when verification is not under test.
- Expose `IIdentityFixture` with authenticated clients per role + anonymous client.

**Auth journey path (no debug shortcuts):**

- Register new user → wait for captured verification email → parse link/code from HTML → verify → login.
- Full transcript + committed baselines for API and email side-channel.

Tier 1 remains fail-fast; failure skips all feature/UI tiers.

## Acceptance criteria

- [ ] Tier 1 seeds reusable identities consumed by downstream tests
- [ ] Auth journey test completes register→verify→login using captured email only
- [ ] Seed tier may use debug verify; auth journey test does not
- [ ] Tier 1 failure skips Tier 2+ with reason in report
- [ ] Committed baselines for auth journey scenario
- [ ] Human-readable transcript for both seed and journey flows

## Blocked by

- [07-capturing-email-sender-e2e-api.md](./07-capturing-email-sender-e2e-api.md)

## User stories covered

- 38, 44, 45, 46

## Notes

- Downstream slices (git, MR, discussions, security) depend on seeded roles from this slice.
- Existing debug endpoints (`/account/debug/verify-email`) remain available for seed only.
