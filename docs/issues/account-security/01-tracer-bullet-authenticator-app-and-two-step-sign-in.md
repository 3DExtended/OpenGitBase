<!-- forge: #245 -->

# [slice] mfa-01 — Tracer bullet: authenticator app and two-step sign-in

## Metadata

- Type: AFK
- Status: done

## Parent

PRD discussion #244

## What to build

The thinnest end-to-end path:
1. A user sets up Google Authenticator or Microsoft Authenticator from the settings.
2. The user signs out.
3. The user signs back in with password plus a 6-digit code, both in the web app and through `ogb auth login`.

Everything sits behind `TwoFactor:Enabled`: default `false`, `true` in local compose and tests.

**Not production-ready yet.** Recovery codes come in mfa-02 and admin reset in mfa-06. Production keeps the switch off until mfa-06.

1. **Backend feature `account-security`** (scaffold with `agentGenCli new backend-feature`).
   - **`TotpAlgorithm`** (in-house RFC 6238, no Otp.NET dependency):
     - Generates a 160-bit secret.
     - Builds `otpauth://totp/{issuer}:{username}?secret=…&issuer=…` with Base32 and no padding.
     - SHA1, 6 digits and 30 s are fixed, not configurable.
     - Verifies a code at a given time with a ±1 step window and returns the matched time step.
     - Time comes from `ISystemClock`. The issuer comes from `TwoFactor:Issuer` (default `OpenGitBase`).
   - **`TotpCodes`** accepts a code for a user and stores the matched step in `LastUsedTimeStep` with a conditional update, so a code is used at most once even under concurrent requests.
   - **Migration:**
     - New table `UserTotpAuthenticators`: `UserId`, `SecretProtected`, `ConfirmedAt`, `PendingSecretProtected`, `LastUsedTimeStep`, `CreatedAt`. The pending column holds a setup in progress while a confirmed authenticator stays active.
     - New table `AuthenticationChallenges`: `Id`, `Purpose`, `UserId`, `TokenHash`, `ExpiresAt`, `CreatedAt`. Unique on `(Purpose, TokenHash)`.
     - `UserCredentials` gains `TwoFactorEnabledAt`, `SecondFactorFailedAttempts` and `SecondFactorLockedUntil`.
   - **Secret at rest:** both secret columns are encrypted with `EncryptSecret`, **never** `EncryptEmail`. Both are added to `EncryptionKeyRotation`.
   - **`AuthenticationChallenges`:**
     - Creates a challenge from a random token and stores its SHA-256. Creating the same purpose and token again renews the row instead of adding a second one.
     - Consumes it via a conditional delete that must affect exactly one row.
     - Purges expired rows.
   - **`SecondFactorAttempts`:** the per-account failure counter and lockout.
2. **Sign-in contract.**
   - **`BeginTwoFactorSignInQuery`** is called by `/signin/login`, `/signin/google` and `/signin/apple` (existing-user path) after the primary credential is accepted:
     - 2FA off: session token string and cookie, unchanged.
     - 2FA on: `{ twoFactorRequired: true, challengeToken, methods: ["totp"] }`, no cookie, 5-minute challenge.
   - **`POST /signin/two-factor`** with `{ challengeToken, code }`:
     - Returns the session token string and cookie.
     - Issues the `role=admin` claim only here.
     - Rejects a replayed code via `LastUsedTimeStep`.
     - `401 { error: "invalid_code" | "challenge_expired" }`, `429 { error: "locked_out", lockedUntil }`.
   - **Limits:**
     - One per-account counter. Wrong codes count across challenges; the 5th consecutive failure locks the second step for 15 minutes with `429` and consumes the current challenge. A success resets the counter.
     - The `sensitive` rate limiter is added to the Google, Apple and two-factor endpoints.
   - Password reset by email leaves 2FA untouched.
3. **Re-authentication.**
   - `POST /account/reauthenticate` takes the password, plus a TOTP code when 2FA is on. It creates a 10-minute `ReAuthentication` challenge bound to the user and the session `jti`.
   - Accounts without a password instead need a session issued within the last 10 minutes (`ogb_iat_ms`). Without 2FA they cannot re-authenticate explicitly (`400 reauthentication_unavailable`).
   - Wrong codes count toward the same lockout as sign-in.
   - `HasRecentReauthenticationQuery` checks it. Required for TOTP setup and confirm and for disabling 2FA (`403 reauthentication_required`).
4. **Account endpoints.**
   - `POST /account/two-factor/totp/setup` returns `{ otpauthUri, secret }` and stores the pending secret, replacing any earlier pending one.
   - `POST /account/two-factor/totp/confirm` with `{ code }` confirms the authenticator (`400 invalid_code` or `no_pending_setup`). The first confirmation sets `TwoFactorEnabledAt`. Setting up again on a confirmed authenticator replaces it only once the new one is confirmed.
   - `POST /account/two-factor/disable` takes no body: it needs a recent re-authentication of the session, which already asks for password plus code. It removes the authenticator and clears `TwoFactorEnabledAt`.
   - Turning 2FA on or off sets `SessionsValidAfter` and re-issues the caller's token (change-password pattern).
   - `GET /account/security` returns `{ twoFactorAvailable, twoFactorEnabled, totpConfigured, hasPassword }`. `/account/me` gains `twoFactorEnabled`.
   - With `TwoFactor:Enabled=false`, setup and confirm return `404`. Disable and the second sign-in step stay available.
   - Structured log events: 2FA enabled, 2FA disabled, second-factor lockout.
5. **Web.**
   - `useAuth`, `cli/auth.vue` and the MSW handlers handle both login answers (token string or challenge).
   - **`sign-in.vue` second step:**
     - A 6-digit field: `autocomplete="one-time-code"`, `inputmode="numeric"`, accepts paste.
     - Error and lockout messages, and a way back to the password step.
   - **New `settings/security.vue`**, linked from the settings navigation:
     - 2FA status.
     - Setup dialog with a QR code generated in the browser by a bundled library (`uqr`, never a remote QR service), plus the setup key as text and a confirmation field.
     - Disable flow and the re-authentication dialog. An action answered with `reauthentication_required` opens the dialog and runs again afterwards.
     - The 2FA section is hidden when the switch is off.
   - `cli/auth.vue` shows the same second step before redirecting to the loopback callback.
   - i18n keys under `auth` and `settings`.
6. **CLI.** The `ogb auth login` loopback timeout goes from 30 s to 5 minutes.
7. **Test infrastructure.** `ControllerTestBase` and the E2E `TestIsolation` login helpers keep working unchanged, because the login answer without 2FA did not change.

Demo, in local compose:
1. Scan the QR code with Microsoft Authenticator or Google Authenticator.
2. Sign out, then sign in with password plus code, in the web app and via `ogb auth login`.
3. Enter a wrong code five times and the lockout message appears.

## Deviations from the original slice

- **Login answer without 2FA** stays the bare token string instead of becoming `{ token }`. Only the 2FA case returns an object, which the web client recognises. No existing consumer had to change.
- **Module shape:** no `ITotpService`, `IAuthenticationChallengeStore` or `SecondFactorGate` interfaces. The logic sits in CQRS query handlers with internal static helpers (`TotpAlgorithm`, `TotpCodes`, `AuthenticationChallenges`, `SecondFactorAttempts`, `Reauthentications`). The TOTP code is in-house rather than Otp.NET.
- **Schema:** `UserTotpAuthenticators` has an extra `PendingSecretProtected` column (replace-on-confirm), and `AuthenticationChallenges` has no `FailedAttempts` column.
- **Attempt limits:** no per-challenge counter. The per-account counter consumes the challenge at the 5th consecutive failure, so "5 wrong codes end the challenge" still holds and failures cannot be reset by signing in again.
- **Disable** relies on a recent re-authentication instead of password plus code in the request body. There is no separate `DELETE /account/two-factor/totp` yet; it belongs to the last-factor rule in mfa-05.

## Acceptance criteria

- [x] TOTP service passes the RFC 6238 Appendix B SHA1 vectors; ±1 step is accepted, ±2 rejected; the otpauth URI carries label, issuer and an unpadded Base32 secret
- [x] With 2FA on, `/signin/login`, `/signin/google` and `/signin/apple` set no cookie and return a challenge; the challenge token is rejected as a bearer token everywhere
- [x] A correct code completes sign-in; the same code reused within its window is rejected; a challenge older than 5 minutes is rejected
- [x] The admin claim only appears in the token issued after the second step
- [x] 5 consecutive wrong codes lock the second step for 15 minutes with `429` and end the challenge; a success resets the counter
- [x] An abandoned setup has no effect on sign-in; setting up again replaces the authenticator only after confirmation
- [x] Setup, confirm and disable require re-authentication; a re-authentication made in one session is rejected in another
- [x] Turning 2FA on or off revokes other sessions and keeps the caller signed in
- [x] Password reset by email does not disable 2FA
- [x] The TOTP secret is stored via `EncryptSecret` and re-protected by `EncryptionKeyRotation` (test)
- [x] `TwoFactor:Enabled=false`: setup and confirm return `404` and the section is hidden; disable and the second step still work for accounts with 2FA on
- [x] `ogb auth login` waits 5 minutes; CLI sign-in with 2FA works through the browser
- [x] Compose E2E: enable TOTP, sign out, sign in with a computed code
- [x] Playwright visual snapshots: sign-in second step, security page, setup dialog
- [x] Controller and query-handler tests satisfy the coverage meta-tests

## Blocked by

None

## User stories covered

- 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 13, 14, 15, 16, 17, 19, 20, 21, 42, 44, 46, 47, 49
