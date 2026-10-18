# Account security — slices

Parent PRD: [#244](../../prd/account-security-optional-two-factor-authentication-and-passkeys.md)

Vertical tracer-bullet slices. `TwoFactor:Enabled` stays off in production until mfa-06; passkeys stay off until `WebAuthn:RelyingPartyId` is set there.

| ID | Title | Type | Status | Blocked by | Forge |
|----|-------|------|--------|------------|-------|
| mfa-01 | [Tracer bullet: authenticator app and two-step sign-in](01-tracer-bullet-authenticator-app-and-two-step-sign-in.md) | AFK | done | — | [#245](https://api.opengitbase.com/opengitbase/open-git-base/discussions/245) |
| mfa-02 | [Recovery codes](02-recovery-codes.md) | AFK | ready | mfa-01 (#245) | [#246](https://api.opengitbase.com/opengitbase/open-git-base/discussions/246) |
| mfa-03 | [Passkey registration and management](03-passkey-registration-and-management.md) | AFK | ready | mfa-01 (#245) | [#247](https://api.opengitbase.com/opengitbase/open-git-base/discussions/247) |
| mfa-04 | [Sign in with a passkey](04-sign-in-with-a-passkey.md) | AFK | ready | mfa-03 (#247) | [#248](https://api.opengitbase.com/opengitbase/open-git-base/discussions/248) |
| mfa-05 | [Passkey as second factor and passkey-only 2FA](05-passkey-as-second-factor-and-passkey-only-2fa.md) | AFK | ready | mfa-02 (#246), mfa-04 (#248) | [#249](https://api.opengitbase.com/opengitbase/open-git-base/discussions/249) |
| mfa-06 | [Notifications, admin reset and production rollout](06-notifications-admin-reset-and-production-rollout.md) | HITL | ready | mfa-05 (#249) | [#250](https://api.opengitbase.com/opengitbase/open-git-base/discussions/250) |
