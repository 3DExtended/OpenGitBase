<!-- forge: #248 -->

# [slice] mfa-04 — Sign in with a passkey

## Metadata

- Type: AFK
- Status: done (the manual cross-device check is still open, see *Closing note*)

## Parent

PRD discussion #244

## What to build

Passwordless sign-in with a registered passkey. It is a complete sign-in: a user-verified passkey satisfies 2FA, so there is no second step.

1. **Backend.**
   - **`POST /signin/passkey/options`** returns request options with:
     - an empty `allowCredentials` (discoverable credentials, so no username is needed and none is revealed);
     - user verification `required`.

     The challenge is single-use (`PasskeySignIn`, no user, 5 minutes). `AuthenticationChallenges.UserId` is nullable for it (migration `AllowAnonymousChallenges`); each new anonymous challenge removes the expired ones, since no later request of a user cleans them up.
   - **`POST /signin/passkey`** with `{ assertion }` (see Deviations):
     - Finds the open challenge through the challenge in `clientDataJSON`, then the credential by id.
     - Verifies signature, origin, RP ID hash, challenge, user handle and the user-verification flag, through the same `PasskeyCeremonies.VerifyAssertionAsync` as re-authentication. Without a user it accepts any registered passkey and reports its owner.
     - Checks the sign counter only when the authenticator increments it; a regressed counter is rejected, a constant 0 (synced passkeys) is accepted.
     - Updates `SignCount`, `IsBackedUp` and `LastUsedAt`.
     - Issues the session token and cookie through `SessionIssuer`, including the admin claim for admins.
   - **Never asks for a second step**, even with 2FA on.
   - An unknown credential, a deleted user, a failed verification and a used or expired challenge all return the same `401 { error: "passkey_rejected" }`.
   - Both endpoints use the `sensitive` rate limiter and return `404` while `WebAuthn:RelyingPartyId` is empty. Failures do not touch the per-account second-factor counter, because there is no account to attribute them to before verification.
   - Structured log events: "Passkey sign-in for user {UserId}", "Passkey sign-in rejected: unknown credential", and the rejection reason for a known credential or a deleted account.
2. **Web.**
   - **`sign-in.vue`:**
     - A "Sign in with a passkey" button below the password form (`AuthPasskeySignInButton`).
     - Conditional mediation: when `PublicKeyCredential.isConditionalMediationAvailable()` is true, `navigator.credentials.get({ mediation: 'conditional' })` starts on page load, with `autocomplete="username webauthn"` on the username field.
     - The pending request is aborted when the user submits the password form or clicks the button. After a failed password sign-in, a rejected passkey or a cancelled prompt, autofill is offered again.
     - A cancelled prompt shows no message; a rejected passkey and `429` have their own messages.
     - Everything stays hidden when WebAuthn is unavailable or the options endpoint answers `404`.
   - **`cli/auth.vue`:** the same button and autofill; on success, it redirects to the loopback callback with the session token.
   - The flow lives in `utils/passkeySignIn.ts` (unit-tested without a browser) with the composable `usePasskeySignIn`.

Demo:
- On localhost, the username field offers the passkey, Touch ID confirms it, and the user is signed in.
- With 2FA on, no code is asked for.
- `ogb auth login` completes with a passkey.

## Deviations from the original slice

- **`POST /signin/passkey` takes `{ assertion }`, without a `challengeToken`.** The WebAuthn challenge the browser signs already identifies the challenge row (its SHA-256 is the `TokenHash`), as in mfa-03. A second token would add nothing.
- **New migration `AllowAnonymousChallenges`** makes `AuthenticationChallenges.UserId` nullable, instead of changing `AddPasskeys`, which is already on `main`. Its `Down` deletes open anonymous challenges before the column becomes required again.
- **Conditional UI in Playwright** relies on Chromium's virtual authenticator answering a conditional request at once, standing in for the user picking the passkey from the autofill menu. The menu itself is browser UI, outside the page, so it belongs to the manual check.

## Acceptance criteria

- [x] Passkey sign-in with the software authenticator issues a session token and cookie; admins get the admin claim
- [x] With 2FA on, passkey sign-in completes without a second step
- [x] Rejected with the same `401`: unknown credential, wrong origin, wrong RP ID, reused or expired challenge, missing user verification, deleted user
- [x] A regressed non-zero sign counter is rejected; a constant zero counter (synced passkeys) is accepted
- [x] The options endpoint reveals nothing about existing usernames
- [x] Playwright with the virtual authenticator: button sign-in and conditional-UI sign-in on `sign-in.vue`, and CLI sign-in on `cli/auth.vue`
- [ ] Manual check documented in the slice closing note: cross-device sign-in via phone QR code (hybrid) on a desktop browser
- [x] Controller and query-handler tests satisfy the coverage meta-tests

## Closing note

The cross-device check needs a phone and a person: open the sign-in page on a desktop browser, choose "Sign in with a passkey", pick "Use a phone or tablet", scan the QR code, and confirm on the phone. It also covers the browser's own autofill menu for conditional UI. It cannot run before passkeys are switched on for a reachable origin (mfa-06 in production, or a local stack opened as `http://localhost`), and has not been done yet.

## Blocked by

- #247 (mfa-03)

## User stories covered

- 34, 35, 36, 37, 39
