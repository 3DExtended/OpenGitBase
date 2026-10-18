<!-- forge: #248 -->

# [slice] mfa-04 — Sign in with a passkey

## Metadata

- Type: AFK
- Status: ready

## Parent

PRD discussion #244

## What to build

Passwordless sign-in with a registered passkey. It is a complete sign-in: a user-verified passkey satisfies 2FA, so there is no second step.

1. **Backend.**
   - **`POST /signin/passkey/options`** returns request options with:
     - an empty `allowCredentials` (discoverable credentials, so no username is needed and none is revealed);
     - user verification `required`.

     The challenge is single-use (`PasskeySignIn`, no user, 5 minutes).
   - **`POST /signin/passkey`** with `{ challengeToken, assertion }`:
     - Finds the credential by id.
     - Verifies signature, origin, RP ID hash, challenge and the user-verification flag.
     - Checks the sign counter only when the authenticator increments it; a regressed counter is rejected.
     - Updates `SignCount` and `LastUsedAt`.
     - Issues the session token and cookie, including the admin claim for admins.
   - **Never asks for a second step**, even with 2FA on.
   - An unknown credential, a deleted user or a failed verification all return the same `401`.
   - Uses the `sensitive` rate limiter. Failures do not touch the per-account second-factor counter, because there is no account to attribute them to before verification.
   - Structured log event: passkey sign-in.
2. **Web.**
   - **`sign-in.vue`:**
     - A "Sign in with a passkey" button.
     - Conditional mediation: when `PublicKeyCredential.isConditionalMediationAvailable()` is true, start `navigator.credentials.get({ mediation: 'conditional' })` on page load, with `autocomplete="username webauthn"` on the username field.
     - Abort the pending request when the user submits the password form or clicks the button.
     - Hide everything when WebAuthn is unavailable or passkeys are disabled.
   - **`cli/auth.vue`:** the same button; on success, redirect to the loopback callback with the session token.

Demo:
- On localhost, the username field offers the passkey, Touch ID confirms it, and the user is signed in.
- With 2FA on, no code is asked for.
- `ogb auth login` completes with a passkey.

## Acceptance criteria

- [ ] Passkey sign-in with the software authenticator issues a session token and cookie; admins get the admin claim
- [ ] With 2FA on, passkey sign-in completes without a second step
- [ ] Rejected with the same `401`: unknown credential, wrong origin, wrong RP ID, reused or expired challenge, missing user verification, deleted user
- [ ] A regressed non-zero sign counter is rejected; a constant zero counter (synced passkeys) is accepted
- [ ] The options endpoint reveals nothing about existing usernames
- [ ] Playwright with the virtual authenticator: button sign-in and conditional-UI sign-in on `sign-in.vue`, and CLI sign-in on `cli/auth.vue`
- [ ] Manual check documented in the slice closing note: cross-device sign-in via phone QR code (hybrid) on a desktop browser
- [ ] Controller and query-handler tests satisfy the coverage meta-tests

## Blocked by

- #247 (mfa-03)

## User stories covered

- 34, 35, 36, 37, 39
