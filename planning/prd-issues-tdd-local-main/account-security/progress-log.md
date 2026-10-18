# Progress Log — Account security

Sequential run log for `/prd-issues-tdd-local-main` over PRD #244 (slices #245–#250).

| When | Item | Action | Result |
|------|------|--------|--------|
| 2026-10-18 | mfa-01 | Feature `account-security`: in-house RFC 6238, single-use codes via `LastUsedTimeStep`, DB-backed challenges, per-account lockout, session-bound re-authentication, migration `AddAccountSecurity`, both secrets in `EncryptionKeyRotation` | 11fa233 |
| 2026-10-18 | mfa-01 | Two-step sign-in on login/Google/Apple + `POST /signin/two-factor`, account security endpoints, `settings/security.vue` with client-side QR, second step in `sign-in.vue` and `cli/auth.vue`, `ogb auth login` waits 5 minutes, visual snapshots | 69e6af0 |
| 2026-10-18 | mfa-01 | Compose E2E: tier 1 Auth 13/13 (incl. TOTP round trip), auth/security regression matrices 53/53; `/account/me` baselines gained `twoFactorEnabled`; manual browser check of setup, sign-in and CLI page against compose | 7be3a71 |
| 2026-10-18 | mfa-01 | Production deploy of `4094ac1`: `AddAccountSecurity` applied, both APIs and both web replicas recreated and verified through the front door; switch off | 4094ac1 |
| 2026-10-18 | mfa-02 | `RecoveryCodes` (PasswordHasher hashes only, normalised input, conditional single use), codes on first confirm, recovery code in the second step and in re-authentication, regeneration, count, disable deletes the set, migration `AddRecoveryCodes`; API contract and controller tests | f4662d4 |
| 2026-10-18 | mfa-02 | Recovery-codes panel, count and low warning, "Use a recovery code" in sign-in, CLI sign-in and re-authentication, visual snapshots | 3fdf82c |
| 2026-10-18 | mfa-02 | Compose E2E: tier 1 14/14 incl. recovery-code sign-in; forge #246 and #244 updated, implementation comment on #246 | 7c52265 |
| 2026-10-18 | mfa-03 | `PasskeyCeremonies` over Fido2NetLib 4.3.1, ceremony options in `AuthenticationChallenges.Payload`, registration, list, rename, delete, passkey re-authentication, migration `AddPasskeys`, `PasskeysController` behind `WebAuthn:RelyingPartyId`; `SoftwarePasskeyAuthenticator` runs the real verification in feature and API tests | 0e39a9e |
| 2026-10-18 | mfa-03 | Passkeys card (synced badge, inline rename, delete confirmation), "Use a passkey" in the re-authentication dialog, `utils/webauthn.ts`, Playwright with Chromium's virtual authenticator, visual snapshots | d2485fd |
| 2026-10-18 | mfa-03 | Compose E2E: tier 1 15/15 incl. passkey registration and re-authentication on Postgres; manual Chromium virtual-authenticator check against the stack; forge #247 and #244 updated, implementation comment on #247 | b197593 |
| 2026-10-18 | mfa-04 | `StartPasskeySignIn`/`CompletePasskeySignIn` (anonymous `PasskeySignIn` challenge, any registered passkey of a live account, same `401` for every failure), migration `AllowAnonymousChallenges`, `POST /signin/passkey/options` and `POST /signin/passkey` through `SessionIssuer` | 22fcc6e |
| 2026-10-18 | mfa-04 | "Sign in with a passkey" and autofill (conditional mediation) on `sign-in.vue` and `cli/auth.vue`, `utils/passkeySignIn.ts`, Playwright with Chromium's virtual authenticator, visual snapshots | 2602b96 |
| 2026-10-18 | mfa-04 | Compose E2E: tier 1 16/16 incl. passkey sign-in with 2FA on; manual Chromium check of button, autofill and CLI sign-in against the stack; forge #248 and #244 updated, implementation comment on #248 | 3fe0f77 |
