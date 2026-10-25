# Execution plan: Account security (2FA and passkeys)

**Source PRD:** [docs/prd/account-security-optional-two-factor-authentication-and-passkeys.md](../../../docs/prd/account-security-optional-two-factor-authentication-and-passkeys.md)  
**Work items:** [docs/issues/account-security/README.md](../../../docs/issues/account-security/README.md)  
**Parent forge:** [#244](https://api.opengitbase.com/opengitbase/open-git-base/discussions/244)

Branch strategy: one worktree branch per slice (`claude/obg-multifactor-login-699197` for mfa-01 to mfa-06), pushed to `main` once green.

## Dependency order

| Order | ID | Title | Type | Status | Blocked by | Forge | Commit |
|-------|-----|-------|------|--------|------------|-------|--------|
| 1 | mfa-01 | Tracer bullet: authenticator app and two-step sign-in | AFK | done | — | #245 | 11fa233, 69e6af0, 7be3a71 |
| 2 | mfa-02 | Recovery codes | AFK | done | mfa-01 | #246 | f4662d4, 3fdf82c, 7c52265 |
| 3 | mfa-03 | Passkey registration and management | AFK | done | mfa-01 | #247 | 0e39a9e, d2485fd, b197593 |
| 4 | mfa-04 | Sign in with a passkey | AFK | done | mfa-03 | #248 | 22fcc6e, 2602b96, 3fe0f77 |
| 5 | mfa-05 | Passkey as second factor and passkey-only 2FA | AFK | done | mfa-02, mfa-04 | #249 | 054cf95, 2afd163, 1acca0c |
| 6 | mfa-06 | Notifications, admin reset and production rollout | HITL | in progress (rollout open) | mfa-05 | #250 | 0cc62b4, ad69f3a |

## Notes

- `TwoFactor:Enabled` stays off in production until the mfa-06 rollout; no account can lock itself out without a recovery path before then.
- `WebAuthn:RelyingPartyId` stays empty in production until the mfa-06 rollout as well, which sets `opengitbase.com` and the origins. The rollout is Peter's (Tower override); the steps are in [account-security-operations.md](../../../docs/deployment/account-security-operations.md).
- Check the "Blocked by" sections on the forge before starting a slice; this table only mirrors them.
