# mfa-01 — Tracer bullet: authenticator app and two-step sign-in

## Summary

Optional two-factor sign-in with an authenticator app (Google/Microsoft Authenticator, any TOTP app), behind `TwoFactor:Enabled` (default off; on in tests and `docker-compose.e2e.yml`):

- **Feature `features/account-security`:** `TotpAlgorithm` (in-house RFC 6238: SHA1, 6 digits, 30 s, ±1 step, unpadded Base32, otpauth URI), `TotpCodes` (single use through a conditional update of `LastUsedTimeStep`), `AuthenticationChallenges` (SHA-256 token hash, unique per purpose, consumed by a conditional delete), `SecondFactorAttempts` (5 consecutive failures → 15-minute lockout, counted across challenges and re-authentications), `Reauthentications` (10 minutes, bound to user and session `jti`). Query handlers for setup, confirm, begin/complete two-factor sign-in, re-authenticate, recent re-authentication, disable and status.
- **Schema:** migration `20261018130204_AddAccountSecurity`: `UserTotpAuthenticators` (with `PendingSecretProtected` for replace-on-confirm), `AuthenticationChallenges`, and on `UserCredentials` `TwoFactorEnabledAt`, `SecondFactorFailedAttempts`, `SecondFactorLockedUntil`. Both secret columns use `EncryptSecret` and are re-protected by `EncryptionKeyRotation`.
- **API:** login, Google and Apple (existing users) return a challenge instead of a cookie when 2FA is on; `POST /signin/two-factor` issues the session (admin claim only here). `GET /account/security`, `POST /account/reauthenticate`, `POST /account/two-factor/totp/setup|confirm`, `POST /account/two-factor/disable`; enable/disable revoke other sessions and re-issue the caller's token. `/account/me` has `twoFactorEnabled`. `sensitive` rate limiter on Google, Apple and two-factor.
- **Web:** second step in `sign-in.vue` and `cli/auth.vue` (`TwoFactorCodeForm`), `settings/security.vue` with status card, setup panel (QR via `uqr` in the browser, setup key), re-authentication dialog that repeats the action, disable confirmation.
- **CLI:** `ogb auth login` loopback timeout 5 minutes.

### Deviations from the slice text

See the "Deviations" section of #245: bare token string kept without 2FA; static helpers instead of service interfaces; `PendingSecretProtected`; no per-challenge counter; disable needs a recent re-authentication instead of password and code in the body; `DELETE /account/two-factor/totp` moved to mfa-05.

## Linked Context

- PRD: `docs/prd/account-security-optional-two-factor-authentication-and-passkeys.md` (forge #244)
- Work item: `mfa-01` (forge #245)

## Dependency Graph

### Direct dependencies (blocked by)

- None

### Full chain

`mfa-01`

## Status

- Branch: `claude/obg-multifactor-login-699197`, pushed to `main`
- Production: deployed 2026-10-18 ~20:50 CEST (`4094ac1`; migration applied, api-1/api-2 and web-1/web-2 recreated, `POST /api/signin/two-factor` answers `challenge_expired` on both APIs); `TwoFactor:Enabled` stays off
- Tests:
  - `dotnet test` (whole solution) green after merging `origin/main`; `OpenGitBase.Api.Tests` 988 passed
  - Web: `pnpm test` 163 passed; `pnpm test:visual` green
  - Compose E2E (images built from the branch): tier 1 Auth 13/13 incl. `TwoFactorTotpE2eTests.EnableTotp_SignOut_SignInWithComputedCode`; `Tag=Regression` auth/security matrices 53/53
  - Manual browser check against compose: setup with re-authentication, sign-out, sign-in second step (wrong code, code with a space), `cli/auth.vue` forwards to the loopback callback
- Visual snapshots: `tests/visual/two-factor.spec.ts` (sign-in second step, security status, TOTP setup); updated `shell.spec.ts` baselines (settings, SSH keys, gallery)
- Commit(s): `11fa233`, `69e6af0`, `7be3a71`

### Local compose note

The worktree's gitignored `docker-compose.override.yml` moved HAProxy to 18089 (`OPENGITBASE_E2E_HTTP_PORT=18089`; 8089 is taken on the dev machine) and replaced the Tower-only Kafka bind mounts with named volumes. The runner does not build images, so they were built first (`docker compose … build`, which needs `.deploy-patch` in the worktree). api-1 was started alone before the runner, because two replicas migrating a fresh database at once race (`42P07 relation "Users" already exists`).
