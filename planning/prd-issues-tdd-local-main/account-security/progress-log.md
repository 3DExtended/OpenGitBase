# Progress Log — Account security

Sequential run log for `/prd-issues-tdd-local-main` over PRD #244 (slices #245–#250).

| When | Item | Action | Result |
|------|------|--------|--------|
| 2026-10-18 | mfa-01 | Feature `account-security`: in-house RFC 6238, single-use codes via `LastUsedTimeStep`, DB-backed challenges, per-account lockout, session-bound re-authentication, migration `AddAccountSecurity`, both secrets in `EncryptionKeyRotation` | 11fa233 |
| 2026-10-18 | mfa-01 | Two-step sign-in on login/Google/Apple + `POST /signin/two-factor`, account security endpoints, `settings/security.vue` with client-side QR, second step in `sign-in.vue` and `cli/auth.vue`, `ogb auth login` waits 5 minutes, visual snapshots | 69e6af0 |
| 2026-10-18 | mfa-01 | Compose E2E: tier 1 Auth 13/13 (incl. TOTP round trip), auth/security regression matrices 53/53; `/account/me` baselines gained `twoFactorEnabled`; manual browser check of setup, sign-in and CLI page against compose | 7be3a71 |
| 2026-10-18 | mfa-01 | Production deploy of `4094ac1`: `AddAccountSecurity` applied, both APIs and both web replicas recreated and verified through the front door; switch off | 4094ac1 |
