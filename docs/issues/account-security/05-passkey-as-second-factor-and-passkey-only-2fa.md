<!-- forge: #249 -->

# [slice] mfa-05 — Passkey as second factor and passkey-only 2FA

## Metadata

- Type: AFK
- Status: done

## Parent

PRD discussion #244

## What to build

Passkeys become a second factor. With 2FA on, users can confirm a password sign-in with a passkey instead of a code. Users can also turn 2FA on with passkeys only, without an authenticator app.

1. **Backend.**
   - **Second step:**
     - `methods` includes `passkey` when the user has at least one passkey and passkeys are switched on. The order is `totp`, `passkey`, `recovery`, as far as the account has them.
     - **`POST /signin/two-factor/passkey-options`** with `{ challengeToken }`:
       - returns request options with `allowCredentials` set to the user's passkeys and user verification `preferred`, so a security-key touch is enough after the password;
       - stores the options as a single-use `PasskeySecondFactor` challenge for the user (5 minutes);
       - returns `401 challenge_expired` for an unknown, used or expired sign-in challenge, `400 no_passkeys`, and `404` while passkeys are off.
     - **`POST /signin/two-factor`** also accepts `{ challengeToken, passkeyAssertion }`; exactly one of `code`, `recoveryCode` and `passkeyAssertion` is allowed.
       - An assertion from another account, a wrong origin or a bad signature returns `401 passkey_rejected`. It counts toward the per-account lockout, which also ends the challenge at the 5th failure (as built in mfa-01).
       - A used or expired passkey challenge returns `401 challenge_expired` and is not counted.
   - **`POST /account/two-factor/enable`:**
     - **Requires:** re-authentication, at least one passkey, `TwoFactor:Enabled` and passkeys switched on.
     - **On success:**
       - sets `TwoFactorEnabledAt`;
       - returns a fresh set of 10 recovery codes once;
       - revokes every session and re-issues the caller's, as the first authenticator confirmation does.
     - **Errors:** `403 reauthentication_required`, `400 no_passkeys`, `409 two_factor_already_enabled`, and `404` while either switch is off.
   - **Last-factor rule:** while 2FA is on, deleting the last passkey of an account without an authenticator app returns `409 { error: "last_second_factor" }`. Disabling 2FA is the way out.
   - **Re-authentication:**
     - Disabling 2FA takes no body and needs a recent re-authentication (mfa-01).
     - `POST /account/reauthenticate` with `{ password, passkeyAssertion }` takes the passkey in place of the code; any of the user's passkeys counts, with or without user verification. A wrong password is `401 invalid_credentials`.
     - On its own, as in mfa-03, the assertion must still be user-verified.
     - Passkeys remain as sign-in methods after 2FA is disabled.
   - **One verification path:** `PasskeyCeremonies.VerifyAssertionAsync` takes `requireUserVerification`. The re-authentication options now only prefer user verification so that security keys work with the password, so the check reads the UV flag from the signed authenticator data instead of relying on the options.
2. **Web.**
   - **Second step** (`AuthTwoFactorCodeForm`, `sign-in.vue` and `cli/auth.vue`):
     - shows "Use a passkey" next to the code field;
     - for passkey-only accounts, makes the passkey the main action with no code field and keeps "Use a recovery code";
     - lives in `utils/passkeySecondFactor.ts` (unit-tested) with `usePasskeySecondFactor`;
     - a closed prompt shows no message, and an expired sign-in returns to the password form.
   - **Security page:**
     - "Turn on with your passkeys" appears in the two-factor card when passkeys exist and 2FA is off, and the recovery-codes panel follows.
     - With 2FA on, the card lists the passkeys next to the authenticator app.
     - For passkey-only 2FA, the re-authentication dialog asks for the password plus "Use a passkey" (with a hint for security keys) or a recovery code, never for an authenticator code.
   - Deleting the last passkey shows a clear message on `409`.

## Deviations from the original slice

- **No `DELETE /account/two-factor/totp`.** No endpoint removes the authenticator app on its own: replacing it goes through a new setup, and removing it means disabling 2FA. The last-factor rule therefore only guards passkey deletion. An account that wants to move from TOTP to passkeys only turns 2FA off and on again with its passkeys, which also issues new recovery codes.
- **User verification for passkey re-authentication** is now `preferred` in the options and enforced on the UV flag when no password comes along. The original rule is unchanged: on its own, a passkey must verify the user.
- **Switching passkeys off** (`WebAuthn:RelyingPartyId` empty) removes `passkey` from the second step. Accounts with passkey-only 2FA then sign in with a recovery code. Passkeys stay switched off in production until mfa-06, so this matters only for an instance that switches them off again.
- **Known gap:** two concurrent deletions of an account's last two passkeys are not serialized and could both pass the last-factor check.

## Acceptance criteria

- [x] With 2FA on, a passkey assertion completes the second step after the password; another user's credential is rejected
- [x] Wrong passkey assertions in the second step count toward the lockout limits
- [x] A user with passkeys and no TOTP can turn 2FA on; recovery codes are returned once; other sessions are revoked
- [x] Enabling with passkeys requires re-authentication, at least one passkey, and `TwoFactor:Enabled`
- [x] While 2FA is on, deleting the last remaining second factor (TOTP or passkey) returns `409` (passkeys only; see Deviations)
- [x] Disabling 2FA works after re-authenticating with password plus passkey; passkeys still sign in afterwards
- [x] Playwright with the virtual authenticator: password plus passkey second step; enabling passkey-only 2FA
- [x] Controller and query-handler tests satisfy the coverage meta-tests

## Blocked by

- #246 (mfa-02)
- #248 (mfa-04)

## User stories covered

- 11, 43, 45
