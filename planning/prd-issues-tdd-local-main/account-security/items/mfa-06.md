# mfa-06 — Notifications, admin reset and production rollout

## Summary

Security emails, an admin recovery path and one naming scheme for the security log events: the last code before 2FA and passkeys go live. The production rollout itself is HITL and still open.

- **Feature `features/account-security`:**
  - **New queries:**
    - `SendSecurityNotification`: one email per `SecurityNotification` (2FA on, 2FA off, passkey added, recovery code used, second-factor lockout, admin reset).
      - Each email carries the time, the remote IP and `TwoFactor:SecuritySettingsUrl`.
      - It goes to the stored address; none without one.
      - The send runs with its own cancellation. A `None` or an exception logs `security.notification.failed` and still returns `Unit`.
    - `ResetTwoFactor`:
      - looks up the live account by normalized username;
      - removes the authenticator, recovery codes and passkeys;
      - clears `TwoFactorEnabledAt` and the lockout, and revokes sessions;
      - logs the admin id and emails the user without the admin's IP.
  - **Events:** `SecurityEvent` (13 names, ids 7001–7090), `SecurityEventLog.Write` (`{name} user={id} ip={address} {details}`, structured values `SecurityEvent`, `UserId`, `RemoteIp`, `Detail`), and `SecurityNotifications.NotifyAsync`.
  - **Changed handlers:**
    - The TOTP confirm, passkey-only enable, disable, passkey registration, second step, and re-authentication handlers notify after saving.
    - Every handler that logged before now writes `security.*` entries.
    - `PasskeyCeremonies` logs its rejections with a reason.
- **Common:** `IRemoteIpAddressAccessor` and `TwoFactorOptions.SecuritySettingsUrl`.
- **API:**
  - `HttpRemoteIpAddressAccessor` reads the connection address after forwarded headers.
  - `AdminUsersController` serves `POST /admin/users/{username}/two-factor/reset` (`204`, `404`, `403`, `401`).
- **Compose:** the e2e overlay sets `TwoFactor__SecuritySettingsUrl` to the local port, and the override example shows it.
- **Docs:**
  - new `docs/deployment/account-security-operations.md` (configuration, RP ID, time, emails, admin reset, events, rollout, rollback);
  - PROJECT-STATE §3.1, §8 and §9;
  - PRD notification, admin and rollout sections.
- **Schema:** none.

### Deviations from the slice text

See the "Deviations" section of #250:
- passkeys and the admin reset are smoke-tested before the switch, 2FA after it;
- no email for replacing the authenticator or for a recovery code used in re-authentication (both logged);
- no admin IP in the reset email;
- unverified addresses are emailed;
- emails are sent inline, with no queue;
- new: `security.two_factor.signed_in` and `TwoFactor:SecuritySettingsUrl`.

## Linked Context

- PRD: `docs/prd/account-security-optional-two-factor-authentication-and-passkeys.md` (forge #244)
- Work item: `mfa-06` (forge #250)
- Operations: `docs/deployment/account-security-operations.md`

## Dependency Graph

### Direct dependencies (blocked by)

- `mfa-05`

### Full chain

`mfa-01` → `mfa-02`, `mfa-03` → `mfa-04` → `mfa-05` → `mfa-06`

## Status

- Branch: `claude/obg-multifactor-login-699197`, pushed to `main`
- Tests:
  - `dotnet test` (whole solution) green.
    - `OpenGitBase.Features.AccountSecurity.Tests`: 185. New: `SendSecurityNotificationQueryHandlerTests`, `ResetTwoFactorQueryHandlerTests`, and `SecurityEventsTests`, which runs every flow through a recording sender and logger and checks that each entry carries a `security.*` name, the user id and the remote IP.
    - `OpenGitBase.Api.Tests`: 1054. New: `AdminUsersControllerTests`, `HttpRemoteIpAddressAccessorTests`, the enable email, and the lockout email through the capturing sender.
  - Compose E2E (images built from the branch, no migration): tier 1 18/18, incl. the new `TwoFactorAdminResetE2eTests.LockedOutUser_AdminReset_PasswordSignsInAgain`. It runs from the "turned on" email through five wrong codes (`429`, lockout email) and the platform admin's reset (`204`, email) to a password-only sign-in, and checks that the user gets `403` resetting another account.
  - Web: unchanged (no UI in this slice).
- **Production rollout:** open (HITL, Peter); steps in the operations doc. The mfa-04 cross-device passkey check waits for it.
- Commit(s): `0cc62b4`, `ad69f3a`
