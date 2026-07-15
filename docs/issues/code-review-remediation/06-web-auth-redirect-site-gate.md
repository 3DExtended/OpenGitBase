<!-- forge: #59 -->

# Web auth redirect and site gate policy

## Metadata

- ID: sec-06
- Type: HITL
- Status: ready
- Source: code review (Jul 2026)

## What to build

Close open-redirect vulnerabilities on sign-in and site gate, and implement a deliberate site-gate policy for production.

**Decision required (HITL):** Choose site gate approach for production:

- Remove site gate entirely from production builds, or
- Move verification server-side (API or edge), or
- Keep as cosmetic staging-only gate with password not in client bundle (document that it is not a security boundary)

**Behavior (regardless of site gate decision):**

- `redirect` query parameter on sign-in and gate pages accepts only same-origin relative paths (must start with `/`, reject `//`, `http:`, `https:`, and encoded variants).
- Invalid redirect falls back to `/` or dashboard home.
- Site gate implementation matches the chosen HITL policy (no hardcoded password in client JS for production).

## Acceptance criteria

- [ ] Sign-in with `?redirect=/settings` navigates to `/settings` after success
- [ ] Sign-in with `?redirect=//evil.com` or external URL navigates to safe default, not external site
- [ ] Gate page redirect validation matches sign-in rules
- [ ] Site gate policy documented; production build matches decision (removed, server-side, or staging-only)
- [ ] Playwright tests cover allowed and blocked redirect values

## Blocked by

- None — can start immediately (HITL gate is site gate policy)

## Findings covered

- High: open redirect after sign-in and site gate
- High: site gate password hardcoded in client bundle and bypassable via localStorage

## Notes

Redirect validation can ship immediately as AFK work inside this slice; only the site gate policy portion requires HITL sign-off.
