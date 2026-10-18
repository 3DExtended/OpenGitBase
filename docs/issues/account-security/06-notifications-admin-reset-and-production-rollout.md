<!-- forge: #250 -->

# [slice] mfa-06 — Notifications, admin reset and production rollout

## Metadata

- Type: HITL
- Status: ready

## Parent

PRD discussion #244

## What to build

The last pieces before 2FA and passkeys go live in production: security emails, an admin recovery path, consistent security events, operations docs, and the rollout itself.

1. **Security emails** via the existing SendGrid email service, styled like the current verification and reset mails.
   - **Sent for:**
     - 2FA turned on; 2FA turned off;
     - passkey added;
     - recovery code used to sign in;
     - second-factor lockout ("your password was entered correctly but the second step failed repeatedly");
     - admin reset.
   - **Content:** time, remote IP, a link to the security settings.
   - A send failure is logged and never fails the action that triggered it.
2. **Admin reset:** `POST /admin/users/{username}/two-factor/reset` (`role=admin`).
   - Deletes the TOTP authenticator, recovery codes and all passkeys.
   - Clears `TwoFactorEnabledAt` and the lockout fields, and revokes all sessions.
   - Emails the user and logs the event with the admin's user id.
   - Unknown user: `404`.
3. **Security events.** One consistent naming scheme (e.g. `security.two_factor.enabled`) for every structured log event added in mfa-01 to mfa-05, with user id and remote IP.
4. **Operations docs:** new `docs/deployment/account-security-operations.md`.
   - **Configuration:** `TwoFactor:Enabled`, `TwoFactor:Issuer`, `WebAuthn:*`.
   - **The relying-party ID is permanent:** changing the domain invalidates every passkey.
   - Passkeys do not work when the site is opened by IP address.
   - API hosts need NTP.
   - Admin reset with a `curl` example, and how to find the security events in the logs.
   - Also update the identity table in `docs/PROJECT-STATE.md` §3.1.
5. **Production rollout (HITL).** Peter edits the Tower override file; Claude cannot read it.
   1. Set `WebAuthn__RelyingPartyId=opengitbase.com` and `WebAuthn__Origins` (`https://opengitbase.com`, `https://www.opengitbase.com`, `https://api.opengitbase.com`). Confirm the exact origins the web app is served from.
   2. Verify time sync on Tower.
   3. Run the migration via `api-migrate`, then roll api-2, api-1 and web.
   4. Smoke test with a non-admin test account: TOTP setup and sign-in, recovery code, passkey registration and sign-in, admin reset, emails received.
   5. Only then set `TwoFactor__Enabled=true` and roll the APIs again.

## Acceptance criteria

- [ ] Each listed event sends exactly one email to the account's address; a failing email sender does not fail the action (tests with a fake sender)
- [ ] Admin reset removes all second factors and passkeys, revokes sessions, emails the user; non-admins get `403`; unknown user `404`
- [ ] After an admin reset the user signs in with password only and can set up 2FA again
- [ ] Every security event from mfa-01 to mfa-05 uses the common naming scheme (test or log-assertion where practical)
- [ ] Operations doc and PROJECT-STATE are updated
- [ ] Production: passkeys and TOTP work on `opengitbase.com`; `TwoFactor:Enabled` is on; smoke test recorded in the slice closing note
- [ ] Controller and query-handler tests satisfy the coverage meta-tests

## Blocked by

- #249 (mfa-05)

## User stories covered

- 18, 27, 48, 50, 51, 52
