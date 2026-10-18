# mfa-04 — Sign in with a passkey

## Summary

Passwordless sign-in with a registered passkey, without a username and without a second step. Behind `WebAuthn:RelyingPartyId`, like mfa-03:

- **Feature `features/account-security`:** `StartPasskeySignIn` (empty `allowCredentials`, user verification required, anonymous `PasskeySignIn` challenge for 5 minutes) and `CompletePasskeySignIn` (any registered passkey, owner must not be deleted, every failure `Rejected`). `PasskeyCeremonies.VerifyAssertionAsync` takes an optional user and returns the passkey's owner; `AuthenticationChallenges.CreateAsync` takes an optional user and purges expired anonymous rows. `SoftwarePasskeyAuthenticator` gained a settable `SignCount` and `CountsSignatures` for the counter cases.
- **Schema:** migration `20261018203540_AllowAnonymousChallenges` makes `AuthenticationChallenges.UserId` nullable.
- **API:** `POST /signin/passkey/options` and `POST /signin/passkey` with `{ assertion }` in `SignInController` (`sensitive` rate limiter, `404` while the RP ID is empty); the session goes through `SessionIssuer`, so admins get their claim; every failure is `401 passkey_rejected`.
- **Web:** `utils/passkeySignIn.ts` (autofill via conditional mediation, the button, cancelling and resuming), `usePasskeySignIn`, `AuthPasskeySignInButton`, wired into `sign-in.vue` and `cli/auth.vue` with `autocomplete="username webauthn"`; MSW handlers and a gallery section.

### Deviations from the slice text

See the "Deviations" section of #248: `{ assertion }` without a `challengeToken`; a new migration instead of changing `AddPasskeys`; conditional UI in Playwright relies on Chromium's virtual authenticator answering at once. The manual cross-device (hybrid QR) check is still open.

## Linked Context

- PRD: `docs/prd/account-security-optional-two-factor-authentication-and-passkeys.md` (forge #244)
- Work item: `mfa-04` (forge #248)

## Dependency Graph

### Direct dependencies (blocked by)

- `mfa-03`

### Full chain

`mfa-01` → `mfa-03` → `mfa-04`

## Status

- Branch: `claude/obg-multifactor-login-699197`, pushed to `main`
- Tests:
  - `dotnet test` (whole solution) green; `OpenGitBase.Features.AccountSecurity.Tests` 127, `OpenGitBase.Api.Tests` 1025 (new `SignInControllerPasskeyTests`, switch-off and rate-limit pins for both endpoints)
  - Web: `pnpm test` 188 passed (9 new for `passkeySignIn`); `pnpm test:visual` 232 passed (the WebAuthn runs are desktop only, so 14 are skipped on mobile and tablet), including the new `passkey-sign-in.spec.ts`: button, autofill, CLI page, switched off, and a password attempt that cancels and resumes autofill; lint and typecheck with no new findings
  - Compose E2E (images built from the branch, api-1 started alone first, `AllowAnonymousChallenges` checked in `__EFMigrationsHistory` and `\d "AuthenticationChallenges"`): tier 1 16/16 incl. the new `PasskeySignInE2eTests.SignInWithPasskey_TwoFactorOn_NoSecondStep`
  - Manual check against the same stack: Chromium's virtual authenticator registered a passkey through the security page, then three fresh browsers signed in with it through the button, through autofill, and through the CLI page (whose token then worked as a bearer). So Chromium's answer to a usernameless request passes Fido2NetLib; "Passkey sign-in for user" was logged for each.
  - Open: the cross-device (hybrid QR) check with a phone, and the browser's own autofill menu; both need a person
- Visual snapshots: new `visual-passkey-sign-in`; updated `sign-in` (the page now shows the passkey button), `visual-cli-auth` and the gallery baselines
- Commit(s): `22fcc6e`, `2602b96`, `3fe0f77`
