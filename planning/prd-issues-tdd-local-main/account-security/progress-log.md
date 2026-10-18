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
