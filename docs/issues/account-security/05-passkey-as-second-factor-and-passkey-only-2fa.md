<!-- forge: #249 -->

# [slice] mfa-05 — Passkey as second factor and passkey-only 2FA

## Metadata

- Type: AFK
- Status: ready

## Parent

PRD discussion #244

## What to build

Passkeys become a second factor. Users with 2FA on can confirm a password sign-in with a passkey instead of a code, and users can turn 2FA on with passkeys only, without an authenticator app.

1. **Backend.**
   - **Second step:**
     - `methods` includes `passkey` when the user has at least one passkey.
     - `POST /signin/two-factor/passkey-options` with `{ challengeToken }` returns request options with `allowCredentials` set to the user's passkeys and user verification `preferred` (a security-key touch is enough after the password).
     - `POST /signin/two-factor` also accepts `{ challengeToken, passkeyAssertion }`. Failures count toward the challenge and account limits.
   - **`POST /account/two-factor/enable`** (re-authenticated, needs ≥1 passkey, honours `TwoFactor:Enabled`):
     - sets `TwoFactorEnabledAt`;
     - returns a fresh recovery-code set;
     - revokes other sessions.
   - **Last-factor rule:** while 2FA is on, removing the TOTP authenticator or a passkey so that no second factor remains returns `409`. Disabling 2FA is the way out.
   - **Disabling 2FA** accepts a passkey assertion in place of a TOTP code. Passkeys remain as sign-in methods after 2FA is disabled.
2. **Web.**
   - The second step shows "Use a passkey" in `sign-in.vue` and in `cli/auth.vue`.
   - The security page shows "Turn on 2FA with your passkeys" when passkeys exist but no authenticator app. The recovery-codes panel follows.
   - A clear message when deleting a factor is refused with `409`.

## Acceptance criteria

- [ ] With 2FA on, a passkey assertion completes the second step after the password; another user's credential is rejected
- [ ] Wrong passkey assertions in the second step count toward the lockout limits
- [ ] A user with passkeys and no TOTP can turn 2FA on; recovery codes are returned once; other sessions are revoked
- [ ] Enabling with passkeys requires re-authentication, at least one passkey, and `TwoFactor:Enabled`
- [ ] While 2FA is on, deleting the last remaining second factor (TOTP or passkey) returns `409`
- [ ] Disabling 2FA works with password plus passkey; passkeys still sign in afterwards
- [ ] Playwright with the virtual authenticator: password plus passkey second step; enabling passkey-only 2FA
- [ ] Controller and query-handler tests satisfy the coverage meta-tests

## Blocked by

- #246 (mfa-02)
- #248 (mfa-04)

## User stories covered

- 11, 43, 45
