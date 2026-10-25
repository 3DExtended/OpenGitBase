<!-- forge: #250 -->

# [slice] mfa-06 — Notifications, admin reset and production rollout

## Metadata

- Type: HITL
- Status: in progress (code done; the production rollout is open, see *Closing note*)

## Parent

PRD discussion #244

## What to build

The last pieces before 2FA and passkeys go live in production: security emails, an admin recovery path, consistent security events, operations docs, and the rollout itself.

1. **Security emails** via the existing SendGrid email service, styled like the current verification and reset mails.
   - **Sent for:**
     - 2FA turned on (authenticator app or passkeys); 2FA turned off;
     - passkey added;
     - recovery code used to sign in, with the number of unused codes left;
     - second-factor lockout at sign-in or re-authentication ("your password was entered correctly, but the second sign-in step failed 5 times in a row");
     - admin reset.
   - **Content:** time (UTC), remote IP, and a link to the security settings (`TwoFactor:SecuritySettingsUrl`, default `https://opengitbase.com/settings/security`).
   - **Built as** `SendSecurityNotificationQuery`, run by the handlers after their change is saved.
     - It sends to the account's stored address; an account without one gets no email.
     - The send runs with its own cancellation, so an aborted request does not cancel it.
     - A failing or throwing sender is logged as `security.notification.failed` and never fails the action that triggered it.
2. **Admin reset:** `POST /admin/users/{username}/two-factor/reset` (`role=admin`, `AdminUsersController`).
   - **Removes:** the TOTP authenticator, recovery codes and all passkeys.
   - **Clears and revokes:** `TwoFactorEnabledAt` and the lockout fields are cleared, and every session is revoked.
   - **Notifies:** emails the user, and logs `security.two_factor.admin_reset` with the admin's user id.
   - **Answers:**
     - `204` when the reset is done; the username matches in any letter case;
     - `404` when no live account has that username;
     - `403` without `role=admin`;
     - `401` anonymous.
   - Works whether or not `TwoFactor:Enabled` is on.
3. **Security events.** Every log entry of the feature has a `security.<area>.<event>` name (also the `EventId` name, ids 7001–7090), the user id and the remote IP.
   - The message starts with `{name} user={id} ip={address}`, followed by details such as `method=totp` or `step=sign-in`.
   - `IRemoteIpAddressAccessor` reads the client address after the forwarded headers from the trusted proxies.
   - The full list is in the operations doc.
4. **Operations docs:** new `docs/deployment/account-security-operations.md`.
   - **Configuration:** `TwoFactor:*`, `WebAuthn:*`, `SendGrid:*`.
   - **Passkey limits:**
     - the relying-party ID is permanent, and changing the domain invalidates every passkey;
     - passkeys do not work when the site is opened by IP address.
   - **Time:** API hosts need NTP, with a check command.
   - **Emails and admin reset:** the security emails, and the admin reset with a `curl` example.
   - **Logs:** the event table and how to find the events in the API logs.
   - **Rollout and rollback.**
   - The identity table in `docs/PROJECT-STATE.md` §3.1 is updated.
5. **Production rollout (HITL).** Peter edits the Tower override file; Claude cannot read it. The full steps are under *Production rollout* in the operations doc:
   1. **Configuration:**
      - Set `WebAuthn__RelyingPartyId=opengitbase.com` and `WebAuthn__Origins` (`https://opengitbase.com`, `https://www.opengitbase.com`, `https://api.opengitbase.com`).
      - Confirm the exact origins the web app is served from.
      - Check that SendGrid is configured.
   2. Verify time sync on Tower.
   3. Run the migrations via `api-migrate` (`AddRecoveryCodes`, `AddPasskeys`, `AllowAnonymousChallenges`; mfa-06 adds none), then roll api-2, api-1 and web.
   4. Smoke test passkeys with a non-admin test account: registration and sign-in, an admin reset, the emails, and the client IP in them.
   5. Set `TwoFactor__Enabled=true` and roll the APIs again.
   6. Smoke test 2FA right away: TOTP setup and sign-in, a recovery code, a passkey as the second step, turning 2FA off, an admin reset with 2FA on, and the emails.

## Deviations from the original slice

- **Rollout order.** The slice smoke-tested TOTP before turning `TwoFactor:Enabled` on, which cannot work: with the switch off, TOTP setup answers `404`. Passkeys and the admin reset are therefore tested before the switch, and 2FA right after it, with a rollback that turns the switch off again.
- **Emails that are not sent:**
  - Replacing the authenticator app sends no email, because 2FA stays on.
  - A recovery code used for re-authentication is logged (`security.recovery_code.used step=re-authentication`) but not emailed. The slice lists only sign-in.
- **No admin IP in the reset email.** The address is the administrator's, which tells the user nothing; the log entry keeps it.
- **Unverified addresses** get the emails too, as password-reset mails do.
- **Emails are sent inline** in the request, after the change is saved, so a slow SendGrid answer slows the request down but cannot undo it. There is no queue.
- **New:**
  - the event `security.two_factor.signed_in`, with `method=totp`, `recovery` or `passkey`;
  - the setting `TwoFactor:SecuritySettingsUrl`; the e2e overlay points it at the local port.

## Acceptance criteria

- [x] Each listed event sends exactly one email to the account's address; a failing email sender does not fail the action (tests with a fake sender)
- [x] Admin reset removes all second factors and passkeys, revokes sessions, emails the user; non-admins get `403`; unknown user `404`
- [x] After an admin reset the user signs in with password only and can set up 2FA again
- [x] Every security event from mfa-01 to mfa-05 uses the common naming scheme (test or log-assertion where practical)
- [x] Operations doc and PROJECT-STATE are updated
- [ ] Production: passkeys and TOTP work on `opengitbase.com`; `TwoFactor:Enabled` is on; smoke test recorded in the slice closing note
- [x] Controller and query-handler tests satisfy the coverage meta-tests

## Closing note

The code part is on `main`. The production rollout (step 5) is Peter's and has not been done yet. Record its smoke test here when it is done, together with the cross-device passkey check that mfa-04 left open (#248), which needs passkeys switched on for `opengitbase.com`.

## Blocked by

- #249 (mfa-05)

## User stories covered

- 18, 27, 48, 50, 51, 52
