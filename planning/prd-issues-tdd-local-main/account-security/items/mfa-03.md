# mfa-03 — Passkey registration and management

## Summary

Signed-in users register, name, list and delete passkeys and can confirm a security change with one. Behind `WebAuthn:RelyingPartyId` (empty = off; `localhost` in tests and `docker-compose.e2e.yml`):

- **Feature `features/account-security`:** `PasskeyCeremonies` wraps Fido2NetLib (`Fido2` 4.3.1): relying party from `WebAuthnOptions`, ceremony challenges stored with their options JSON in `AuthenticationChallenges.Payload` (5 minutes, single use, bound to the user), assertion verification shared with mfa-04 and mfa-05. Handlers: `StartPasskeyRegistration`, `CompletePasskeyRegistration`, `ListPasskeys`, `RenamePasskey`, `DeletePasskey`, `StartPasskeyReauthentication`, `ReauthenticateWithPasskey`. Structured logs for added, removed and rejected passkeys.
- **Schema:** migration `20261018195024_AddPasskeys`: `UserPasskeys` (`CredentialId` unique, `SignCount` bigint, cascade from `Users`), `AuthenticationChallenges.Payload`.
- **API:** `PasskeysController` (`/account/passkeys`, `/registration-options`, `PATCH`/`DELETE /{id}`; everything `404` while the RP ID is empty); `POST /account/reauthenticate/passkey-options` and `passkeyAssertion` on `POST /account/reauthenticate`; `passkeysAvailable` on `GET /account/security`; the re-authentication check moved into `Services/RecentReauthentication`.
- **Web:** `utils/webauthn.ts` (JSON parsers with base64url fallback, `toJSON()` fallback, feature detection, error mapping), `SettingsPasskeysCard` (list, synced badge, inline rename, add with name), delete confirmation, "Use a passkey" in `ReauthenticateForm`, MSW handlers, gallery section.
- **Test tooling:** `SoftwarePasskeyAuthenticator` (ES256, attestation `none`) in `OpenGitBase.Common.Tests`, linked into `OpenGitBase.E2E.Core`; Playwright with Chromium's virtual authenticator.

### Deviations from the slice text

See the "Deviations" section of #247: static helpers instead of `IPasskeyService`; options JSON in `AuthenticationChallenges.Payload`; separate `PasskeyReauthentication` purpose; `SignCount` as bigint; passwordless accounts keep the recent-session rule after adding a passkey.

## Linked Context

- PRD: `docs/prd/account-security-optional-two-factor-authentication-and-passkeys.md` (forge #244)
- Work item: `mfa-03` (forge #247)

## Dependency Graph

### Direct dependencies (blocked by)

- `mfa-01`

### Full chain

`mfa-01` → `mfa-03`

## Status

- Branch: `claude/obg-multifactor-login-699197`, pushed to `main`
- Tests:
  - `dotnet test` (whole solution) green; `OpenGitBase.Features.AccountSecurity.Tests` 112, `OpenGitBase.Api.Tests` 1013
  - Web: `pnpm test` 179 passed; `pnpm test:visual` 224 passed (the virtual-authenticator test runs on the desktop project only); lint and typecheck with no new findings
  - Compose E2E (images built from the branch, api-1 started alone first for the new migration, `AddPasskeys` checked in `__EFMigrationsHistory` and `\d "UserPasskeys"`): tier 1 15/15 incl. the new `PasskeyE2eTests.RegisterPasskey_ReauthenticateWithIt_ThenRemoveIt`. The `Tag=Regression` matrices were not rerun: the changed routes are new, and `/account/security` only gained a field.
  - Manual check against the same stack: Chromium's virtual authenticator registered a passkey through the security page (password re-authentication first), then a second session deleted it after re-authenticating with that passkey, so the browser's own `toJSON()` output passes Fido2NetLib. Both log events showed up.
- Visual snapshots: new `visual-passkeys`; updated `security` (the page now shows the passkeys card) and the gallery baselines
- Commit(s): `0e39a9e`, `d2485fd`, `b197593`
