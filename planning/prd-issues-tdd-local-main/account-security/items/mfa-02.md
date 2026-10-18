# mfa-02 — Recovery codes

## Summary

One-time recovery codes for a lost phone, the precondition for turning `TwoFactor:Enabled` on in production:

- **Feature `features/account-security`:** `RecoveryCodes` (sets of 10 codes, 10 Base32 characters shown as `xxxxx-xxxxx`, `PasswordHasher` hashes only, input normalised for dash, spaces and case, spent with a conditional update of `UsedAt`). `ConfirmTotpSetupQueryHandler` returns the first set when it turns 2FA on; `CompleteTwoFactorSignInQuery` and `ReauthenticateQuery` take `RecoveryCode`; new `RegenerateRecoveryCodesQuery`; disable deletes the set; `AccountSecurityStatus.RecoveryCodesRemaining`; `methods` includes `recovery` while unused codes exist. Wrong recovery codes feed the same per-account lockout as wrong TOTP codes. Structured log on use (step and remaining count) and on regeneration.
- **Schema:** migration `20261018190549_AddRecoveryCodes`: `UserRecoveryCodes` (`Id`, `UserId` → `Users` cascade, `CodeHash`, `UsedAt`, `CreatedAt`, index on `UserId`).
- **API:** `POST /signin/two-factor` takes exactly one of `code` and `recoveryCode`; `POST /account/reauthenticate` takes `recoveryCode`; confirm returns `recoveryCodes`; `POST /account/two-factor/recovery-codes` (re-authenticated, `400 two_factor_not_enabled`, available with the switch off); `GET /account/security` has `recoveryCodesRemaining`.
- **Web:** `RecoveryCodesPanel` (copy, `.txt` download, "I have stored these codes") after enabling and after regenerating; count, low warning (≤ 2) and "Generate new codes" with a warning on the status card; "Use a recovery code" in `TwoFactorCodeForm` (`sign-in.vue`, `cli/auth.vue`) and `ReauthenticateForm`.

### Deviations from the slice text

See the "Deviations" section of #246: no SHA-256 lookup hash (50-bit codes); re-authentication accepts a recovery code; regeneration stays available with the switch off.

## Linked Context

- PRD: `docs/prd/account-security-optional-two-factor-authentication-and-passkeys.md` (forge #244)
- Work item: `mfa-02` (forge #246)

## Dependency Graph

### Direct dependencies (blocked by)

- `mfa-01`

### Full chain

`mfa-01` → `mfa-02`

## Status

- Branch: `claude/obg-multifactor-login-699197`
- Tests:
  - `dotnet test` (whole solution) green; `OpenGitBase.Features.AccountSecurity.Tests` 80, `OpenGitBase.Api.Tests` 997
  - Web: `pnpm test` 168 passed; `pnpm test:visual` 220 passed; lint and typecheck with no new findings
  - Compose E2E (images built from the branch, api-1 started alone first for the new migration): tier 1 14/14 incl. the new `TwoFactorTotpE2eTests.EnableTotp_SignInWithARecoveryCodeOnlyOnce` (10 codes on confirm, `methods` has `recovery`, upper case without dash accepted, count 9, reuse 401). The `Tag=Regression` matrices were not rerun: they touch neither the security routes nor the second step, and `/account/me` and login without 2FA are unchanged.
- Visual snapshots: new `visual-two-factor-recovery-sign-in`, `visual-recovery-codes`; updated `visual-two-factor-sign-in`, `visual-security-status`, `visual-totp-setup` and the gallery baselines
- Commit(s): `f4662d4`, `3fdf82c`
