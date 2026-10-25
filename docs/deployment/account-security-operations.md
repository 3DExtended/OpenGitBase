# Account security operations

How two-factor sign-in (authenticator app, recovery codes) and passkeys are configured and rolled out, how an
administrator resets a user who lost every second factor, and how to find security events in the logs. Design:
[prd/account-security-optional-two-factor-authentication-and-passkeys.md](../prd/account-security-optional-two-factor-authentication-and-passkeys.md)
(forge #244).

## Configuration

All settings are read by the API (`api-1`, `api-2`). In compose they go into the override's `x-api-env` →
`environment` block, so both APIs get the same values.

| Setting | Env var | Default | Meaning |
|---------|---------|---------|---------|
| `TwoFactor:Enabled` | `TwoFactor__Enabled` | `false` | Lets users **turn 2FA on**. Off: TOTP setup/confirm and passkey-only enable answer `404` and the settings page hides the section. Accounts that already have 2FA keep the second step and can still turn it off. |
| `TwoFactor:Issuer` | `TwoFactor__Issuer` | `OpenGitBase` | The name authenticator apps show next to the account. Changing it only affects new setups. |
| `TwoFactor:SecuritySettingsUrl` | `TwoFactor__SecuritySettingsUrl` | `https://opengitbase.com/settings/security` | The link in every security email. |
| `WebAuthn:RelyingPartyId` | `WebAuthn__RelyingPartyId` | empty | The registrable domain passkeys are bound to (`opengitbase.com`). Empty turns passkeys off: their endpoints answer `404`, `passkey` disappears from the second step, and the web app hides them. |
| `WebAuthn:RelyingPartyName` | `WebAuthn__RelyingPartyName` | `OpenGitBase` | The name the browser shows when it creates a passkey. |
| `WebAuthn:Origins` | `WebAuthn__Origins__0`, `__1`, … | none | Every exact origin the web app is served from: scheme, host and port. An origin that is missing here rejects every passkey ceremony started on it. |
| `SendGrid:*` | `SendGrid__ApiKey`, `SendGrid__FromEmailAddress`, `SendGrid__FromSenderName`, `SendGrid__IsDisabled` | disabled | Security emails go through the same SendGrid service as verification and reset mails. |

### Relying-party ID and origins

- **The relying-party ID is permanent.** Every passkey is bound to it. Changing the domain, or the ID from
  `opengitbase.com` to a subdomain, makes every existing passkey unusable. Users then sign in with their password
  and recovery codes and register new passkeys.
- An ID of `opengitbase.com` covers `opengitbase.com` and every subdomain (`www.`, `api.`). Each origin still has
  to be listed in `WebAuthn:Origins`; production uses `https://opengitbase.com`, `https://www.opengitbase.com` and
  `https://api.opengitbase.com`.
- **Passkeys do not work when the site is opened by IP address** (for example `http://192.168.1.10:3000`).
  Browsers only allow a domain as relying party. Use the domain, or `localhost` for local development.
- Local compose uses `WebAuthn__RelyingPartyId=localhost` with the origin `http://localhost:8089`; the e2e overlay
  sets the origin from `OPENGITBASE_E2E_HTTP_PORT`.

### Time

TOTP accepts the current 30-second step and one step either side, and challenges expire after 5 or 10 minutes, so
**API hosts need NTP**. Check the clock on the host against an outside source:

```bash
date -u; curl -sI https://www.cloudflare.com | grep -i '^date:'
```

The two times should agree to within a few seconds. Containers use the host clock.

## Security emails

Each of these events sends exactly one email to the account's address. The email states the time (UTC), the remote
IP and a link to the security settings:

| Event | Subject |
|-------|---------|
| 2FA turned on (authenticator app or passkeys) | Two-factor authentication turned on |
| 2FA turned off | Two-factor authentication turned off |
| Passkey added | Passkey added to your account |
| Recovery code used to sign in | Recovery code used to sign in (with the number of codes left) |
| Second-factor lockout (5 wrong second steps, at sign-in or re-authentication) | Sign-in blocked after failed second steps |
| Admin reset | Two-factor authentication reset by an administrator (without the administrator's IP) |

- Replacing the authenticator app and using a recovery code in a re-authentication are logged but not emailed.
- Accounts without an email address get no email.
- A send failure never fails the action. It is logged as `security.notification.failed` with the reason.
- With SendGrid disabled the API logs `SendGrid disabled; skipping email …` instead.

## Admin reset

For a user who lost the authenticator app, the recovery codes and every passkey. The reset:
- removes the authenticator app, the recovery codes and all passkeys;
- turns 2FA off and clears the lockout;
- revokes every session, and emails the user.

Afterwards the user signs in with the password alone and can set up 2FA again.

**Confirm the request first.** A reset hands the account to whoever knows the password. Make sure the request comes
from the account owner, for example by a reply from the account's email address.

```bash
# An admin session token. With 2FA on for the admin, the answer is a challenge instead: complete it with
# POST /api/signin/two-factor {"challengeToken": "...", "code": "123456"}, which returns the token.
TOKEN=$(curl -s https://api.opengitbase.com/api/signin/login \
  -H 'Content-Type: application/json' \
  -d '{"username":"admin","password":"..."}' | tr -d '"')

curl -i -X POST https://api.opengitbase.com/api/admin/users/<username>/two-factor/reset \
  -H "Authorization: Bearer $TOKEN"
```

- `204` the reset is done;
- `404` no live account has that username (any letter case matches);
- `403` the token has no `role=admin`;
- `401` no or an expired token.

API paths go through the load balancer with `/api/` in front; without it the request reaches the web app. On Tower
the same works through the front door: `http://localhost:3000/api/admin/users/<username>/two-factor/reset`. The
reset works whether or not `TwoFactor:Enabled` is on.

## Security events in the logs

Every security event is one log entry whose message starts with its name, followed by the user id, the remote IP and
details:

```
warn: OpenGitBase.Features.AccountSecurity.QueryHandlers.CompleteTwoFactorSignInQueryHandler[7010]
      security.second_factor.locked_out user=3f0c…e1 ip=203.0.113.7 step=sign-in failures=5 locked_until=2026-10-19T09:15:00.0000000+00:00
```

The name is also the event id's name, and `SecurityEvent`, `UserId`, `RemoteIp` and `Detail` are structured values
for log tooling. The IP is the client's: forwarded headers from the trusted proxies (cloudflared, HAProxy) are
applied first.

| Event | Id | Level | Details |
|-------|----|-------|---------|
| `security.two_factor.enabled` | 7001 | Information | `method=totp` or `method=passkeys` |
| `security.two_factor.disabled` | 7002 | Information | |
| `security.two_factor.admin_reset` | 7003 | Warning | `admin=<admin user id>`; the IP is the administrator's |
| `security.two_factor.signed_in` | 7004 | Information | `method=totp`, `recovery` or `passkey` |
| `security.second_factor.locked_out` | 7010 | Warning | `step=sign-in` or `re-authentication`, `failures`, `locked_until` |
| `security.recovery_code.used` | 7020 | Warning | `step`, `remaining` |
| `security.recovery_codes.regenerated` | 7021 | Information | |
| `security.passkey.added` | 7030 | Information | `passkey=<id>` |
| `security.passkey.removed` | 7031 | Information | `passkey=<id>` |
| `security.passkey.signed_in` | 7032 | Information | |
| `security.passkey.rejected` | 7033 | Information | `reason=unknown_credential`, `not_their_passkey`, `user_not_verified`, `account_deleted`, or the verification error |
| `security.passkey.registration_rejected` | 7034 | Information | `reason=<verification error>` |
| `security.notification.failed` | 7090 | Warning | `notification`, `reason` |

The events are only in the API containers' output. Search both, since either may have served the request:

```bash
for api in opengitbase_api_1 opengitbase_api_2; do docker logs -t --since 24h "$api" 2>&1 | grep 'security\.'; done
# One user:
docker logs -t opengitbase_api_1 2>&1 | grep 'security\..*user=<user id>'
```

There is no persistent audit table; the events live as long as the container logs.

## Production rollout

The order, with the steps on Tower run by the operator (the override file holds secrets):

1. **Origins.** Set `WebAuthn__RelyingPartyId: "opengitbase.com"` and `WebAuthn__Origins__0..2` (`https://opengitbase.com`,
   `https://www.opengitbase.com`, `https://api.opengitbase.com`) in the override's `x-api-env`. Confirm the origins
   the web app is really served from: open the site and run `location.origin` in the browser console on each host.
   Check that SendGrid is configured (`SendGrid__IsDisabled` not `true`, API key and sender set), or the security
   emails are only logged as skipped.
2. **Time.** Check the host clock as above.
3. **Deploy.** Run the migrations (`docker compose --profile tools run --rm --no-deps api-migrate`; the account-security
   ones are `AddAccountSecurity`, `AddRecoveryCodes`, `AddPasskeys`, `AllowAnonymousChallenges`). Then roll `api-2`,
   `api-1` and the web replicas.
4. **Smoke test passkeys** with a non-admin test account. `TwoFactor:Enabled` is still off, so 2FA cannot be turned
   on yet, but passkeys work on their own:
   - passkey registration (email "Passkey added") and passkey sign-in;
   - an admin reset (email to the user);
   - the emails carry the client's IP, not an internal one.
5. **Switch on.** Set `TwoFactor__Enabled: "true"` and recreate `api-2`, then `api-1`.
6. **Smoke test 2FA** right away with the same account:
   - TOTP setup and sign-in (email "turned on");
   - a recovery code (email);
   - a passkey as the second step;
   - turning 2FA off (email), then an admin reset of an account with 2FA on.

   If anything fails, switch `TwoFactor__Enabled` off again (see Rollback); accounts that turned 2FA on in the
   meantime keep it.

### Rollback

- `TwoFactor__Enabled: "false"` stops new 2FA setups. Accounts that have 2FA keep the second step and can still turn
  it off, so no account is weakened or locked out.
- An empty `WebAuthn__RelyingPartyId` switches passkeys off. Accounts with passkey-only 2FA then sign in with a
  recovery code, and an admin reset helps those without one.
