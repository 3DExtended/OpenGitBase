# mfa-05 — Passkey as second factor and passkey-only 2FA

## Summary

A passkey answers the second sign-in step after the password, and 2FA can be turned on with passkeys alone. Behind `TwoFactor:Enabled` and `WebAuthn:RelyingPartyId`, like the slices before:

- **Feature `features/account-security`:**
  - **New queries:**
    - `StartTwoFactorPasskey`: options for the open `TwoFactorSignIn` challenge, the user's passkeys, user verification preferred, `PasskeySecondFactor` challenge.
    - `EnableTwoFactorWithPasskeys`: `TwoFactorEnabledAt`, session revocation, fresh recovery codes.
  - **Changed queries:**
    - `CompleteTwoFactorSignIn` takes `PasskeyAssertionJson`; a rejected passkey is `PasskeyRejected` and counts toward the lockout.
    - `BeginTwoFactorSignIn` lists `passkey`.
    - `DeletePasskey` returns `PasskeyDeletion` and refuses the last second factor.
    - `ReauthenticateWithPasskey` takes an optional `Password`.
  - **Shared verification:** `PasskeyCeremonies.VerifyAssertionAsync` gained `requireUserVerification` and checks the UV flag itself. The re-authentication options are therefore only `preferred`.
- **Schema:** none; `PasskeySecondFactor = 6` is a new value of the existing purpose column.
- **API:**
  - `POST /signin/two-factor/passkey-options`.
  - `passkeyAssertion` on `POST /signin/two-factor`.
  - `POST /account/two-factor/enable` (`400 no_passkeys`, `409 two_factor_already_enabled`).
  - `409 last_second_factor` on `DELETE /account/passkeys/{id}`.
  - `{ password, passkeyAssertion }` on `POST /account/reauthenticate`.
- **Web:**
  - `utils/passkeySecondFactor.ts` and `usePasskeySecondFactor`.
  - `AuthTwoFactorCodeForm` gains a passkey mode, used on `sign-in.vue` and `cli/auth.vue`.
  - "Turn on with your passkeys" and the passkey line in `SettingsTwoFactorStatusCard`.
  - `SettingsReauthenticateForm` handles passkey-only 2FA (no code field, the password goes with the passkey).
  - MSW handlers and two gallery sections.

### Deviations from the slice text

See the "Deviations" section of #249:
- no `DELETE /account/two-factor/totp`, so the last-factor rule guards passkeys only;
- re-authentication options prefer user verification and the server enforces it on the UV flag;
- with passkeys switched off, passkey-only accounts fall back to recovery codes;
- concurrent deletion of the last two passkeys is not serialized.

## Linked Context

- PRD: `docs/prd/account-security-optional-two-factor-authentication-and-passkeys.md` (forge #244)
- Work item: `mfa-05` (forge #249)

## Dependency Graph

### Direct dependencies (blocked by)

- `mfa-02`
- `mfa-04`

### Full chain

`mfa-01` → `mfa-02`, `mfa-03` → `mfa-04` → `mfa-05`

## Status

- Branch: `claude/obg-multifactor-login-699197`, pushed to `main`
- Tests:
  - `dotnet test` (whole solution) green; `OpenGitBase.Features.AccountSecurity.Tests` 149, `OpenGitBase.Api.Tests` 1044 (new `SignInControllerTwoFactorPasskeyTests` and `AccountSecurityControllerPasskeyTwoFactorTests`, switch-off and rate-limit pins for `passkey-options` and the passkey answer)
  - Web: `pnpm test` 193 passed (5 new for `passkeySecondFactor`); `pnpm test:visual` 242 passed (the WebAuthn runs are desktop only, so 22 are skipped on mobile and tablet), including the new `passkey-two-factor.spec.ts`: a security-key touch after the password, passkey-only accounts without a code field, the CLI page, and turning 2FA on with passkeys then keeping the last one; lint and typecheck with no new findings
  - Compose E2E (images built from the branch, no migration): tier 1 17/17 incl. the new `PasskeyTwoFactorE2eTests.PasskeyOnlyTwoFactor_PasswordPlusPasskey_SignsIn`. The revocation check uses a fresh client with the old token, because the enabling client already holds the re-issued session cookie, which the API reads before the header
  - Manual check against the same stack with Chromium's virtual authenticators:
    - a passkey registered on a USB key with a PIN through the security page, then 2FA turned on with it (10 codes shown);
    - a second browser with a USB key without user verification signed in with the password plus a touch;
    - deleting the only passkey asked for the password plus the passkey (no code field) and was refused with `409`;
    - the CLI page did the same and its token worked as a bearer.
  - Seen on the way: a passkey's stored transports steer the browser, so one registered on a platform authenticator is not offered to a USB key
- Visual snapshots: new `visual-two-factor-passkey-sign-in` and `visual-security-passkey-two-factor`; updated `security` (the "Turn on with your passkeys" button) and the gallery baselines
- Commit(s): `054cf95`, `2afd163`, `1acca0c`
