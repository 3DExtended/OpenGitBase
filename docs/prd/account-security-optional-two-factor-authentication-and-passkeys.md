<!-- forge: #244 -->

# PRD: Account security — optional two-factor authentication and passkeys

## Problem Statement

An OpenGitBase account is protected by a single factor: a password (or a Google/Apple sign-in). Whoever learns that password gets a full web session, can create personal access tokens, add SSH keys, and push to every repository the account can write to. Admin accounts can additionally manage the storage fleet. Leaked or reused passwords are the most common way accounts are taken over, and today a user has no way to add a second layer.

Users who already carry Google Authenticator or Microsoft Authenticator on their phone expect to be able to use it for OpenGitBase, the same way they do for GitHub, GitLab or their cloud providers. Users on modern devices increasingly expect passkeys: signing in with Face ID, Touch ID, Windows Hello or a security key instead of typing a password at all.

Neither exists today. There is no second-factor code, no TOTP or WebAuthn code anywhere in the codebase, no per-account lockout after failed attempts, and the login endpoint hands out a session as soon as the password matches.

## Solution

Add an **Account security** area with two independent, optional capabilities. Nothing is mandatory: users who do nothing keep signing in exactly as today.

1. **Two-factor authentication (2FA).** A user turns on 2FA in the settings. From then on, every password sign-in (and every Google/Apple sign-in) needs a second step. The second factor can be:
   - a 6-digit code from an **authenticator app** (TOTP, RFC 6238) — works with Google Authenticator, Microsoft Authenticator, Authy, 1Password, Bitwarden and any other TOTP app;
   - a registered **passkey** / security key;
   - a one-time **recovery code**, for when the phone is lost.

2. **Passkeys.** A signed-in user registers one or more passkeys (stored in iCloud Keychain, Google Password Manager, Windows Hello, a password manager, or on a hardware key). A passkey is a complete sign-in on its own: "Sign in with a passkey" asks for Face ID / fingerprint / PIN and the user is in, without a password and without a second step, because a user-verified passkey already proves possession plus biometrics/PIN. When 2FA is on, the same passkey also works as the second factor after a password sign-in.

The `ogb` CLI benefits automatically: `ogb auth login` already signs in through the browser, so the second step and passkey sign-in happen there. Git over HTTPS (personal access tokens) and SSH (keys) are unchanged — they are already non-password credentials, matching how GitHub treats 2FA.

## User Stories

### Authenticator app (TOTP)

1. As a user, I want to set up an authenticator app from my security settings, so that my account needs more than my password.
2. As a user, I want to scan a QR code with Google Authenticator or Microsoft Authenticator, so that setup takes seconds.
3. As a user, I want to see the setup key as text next to the QR code, so that I can type it in when I cannot scan.
4. As a user, I want the entry in my authenticator app to be labelled with the service name and my username, so that I can tell it apart from my other accounts.
5. As a user, I want to confirm setup by entering a code from the app, so that 2FA only turns on once I know the app produces valid codes.
6. As a user, I want an abandoned setup to have no effect, so that closing the dialog halfway never locks me out.
7. As a user, I want codes from the previous or next 30-second window to be accepted, so that small clock differences between my phone and the server do not reject me.
8. As a security reviewer, I want each code to be accepted only once, so that a code observed by an attacker cannot be replayed within its window.
9. As a user, I want to replace my authenticator app (new phone) by setting it up again, so that I can move devices without disabling 2FA.

### Two-factor sign-in

10. As a user with 2FA on, I want to be asked for a code after my password is accepted, so that a stolen password alone does not get anyone in.
11. As a user with 2FA on, I want to use a registered passkey instead of a code in the second step, so that I do not have to open the authenticator app.
12. As a user with 2FA on, I want to use a recovery code in the second step, so that I can still sign in when my phone is lost.
13. As a user with 2FA on, I want the second step to also apply when I sign in with Google or Apple, so that there is no way around 2FA.
14. As a user, I want the code input to accept paste and the browser's one-time-code autofill, so that entering the code is quick.
15. As a user, I want the second step to expire after a few minutes, so that an abandoned half-finished sign-in cannot be completed later by someone else.
16. As a security reviewer, I want the half-finished sign-in to be unusable as a session for any API call, so that passing the password step grants nothing by itself.
17. As a security reviewer, I want the account's second-factor step locked for a while after repeated wrong codes, so that someone who knows the password cannot brute-force the 6-digit code.
18. As a user, I want an email when repeated wrong second-factor attempts lock my account, so that I learn my password is known to someone else.
19. As a user, I want password reset by email to leave 2FA in place, so that access to my mailbox is not enough to take over my account.
20. As a CLI user, I want `ogb auth login` to walk me through the second step in the browser, so that 2FA works for the CLI without extra commands.
21. As a CLI user, I want enough time to type my password and a code before `ogb auth login` gives up, so that the login does not time out under me.

### Recovery codes

22. As a user, I want to receive a set of one-time recovery codes when I turn on 2FA, so that losing my phone does not lock me out forever.
23. As a user, I want to see the recovery codes exactly once and be able to copy or download them, so that I can store them safely.
24. As a user, I want each recovery code to work only once, so that a used code cannot be reused.
25. As a user, I want to see how many unused recovery codes I have left, so that I know when to generate new ones.
26. As a user, I want to generate a new set of recovery codes, invalidating the old set, so that I can recover after using or exposing them.
27. As a user, I want an email when a recovery code is used to sign in, so that I notice if someone else used one.
28. As a security reviewer, I want recovery codes stored only as hashes, so that a database leak does not expose usable codes.

### Passkeys

29. As a user, I want to register a passkey from my security settings, so that I can sign in with Face ID, Touch ID, Windows Hello or a security key.
30. As a user, I want to register several passkeys (laptop, phone, hardware key), so that losing one device does not remove my passkey access.
31. As a user, I want to give each passkey a name and rename it later, so that I know which device each one belongs to.
32. As a user, I want to see when each passkey was created and last used, so that I can spot ones I no longer use.
33. As a user, I want to delete a passkey, so that a lost or retired device can no longer sign in.
34. As a user, I want a "Sign in with a passkey" button on the sign-in page, so that I can sign in without typing my username or password.
35. As a user, I want my browser to offer my passkey right in the username field's autofill, so that passkey sign-in feels like normal sign-in.
36. As a user with 2FA on, I want a passkey sign-in to need no extra code, so that the strongest method is also the fastest.
37. As a user on a desktop without a passkey, I want to use the passkey on my phone via the browser's QR code, so that I can sign in from any computer.
38. As a user who signed up with Google or Apple, I want to add a passkey too, so that I can sign in without the external provider.
39. As a CLI user, I want to use a passkey in the browser window that `ogb auth login` opens, so that the CLI gets the same passwordless sign-in.
40. As a self-hoster, I want to configure the passkey domain and allowed origins, so that passkeys work on my own domain.
41. As a self-hoster, I want passkeys to stay off until that domain is configured, so that a misconfigured instance does not create passkeys bound to the wrong domain.

### Managing 2FA

42. As a user, I want a single security settings page showing whether 2FA is on, which second factors I have, my passkeys, and my remaining recovery codes, so that I understand my account's protection at a glance.
43. As a user, I want to turn on 2FA using only passkeys (without an authenticator app), so that I can use my security key as my second factor.
44. As a user, I want to turn off 2FA after confirming my password and a second factor, so that I stay in control of my account.
45. As a user, I want removing my last second factor to be refused while 2FA is on, so that I cannot accidentally lock myself out.
46. As a user, I want security changes (turning 2FA on or off, adding or removing passkeys, regenerating recovery codes) to ask me to confirm it is really me, so that someone who briefly has my unlocked browser session cannot take over my account.
47. As a user, I want my other sessions signed out when 2FA is turned on or off, so that a session an attacker may already hold stops working.
48. As a user, I want an email whenever 2FA is turned on or off or a passkey is added, so that I notice changes I did not make.
49. As a signed-in user, I want the account information the web app loads to include my 2FA status, so that the UI can show and prompt accordingly.

### Administration

50. As an admin, I want to reset a user's second factors when they have lost both their phone and their recovery codes, so that the account can be recovered after I verified the person out of band.
51. As a user, I want an email when an admin resets my 2FA, so that I learn about it even if I did not ask for it.
52. As an operator, I want security events (2FA on/off, lockouts, recovery-code use, admin resets) written to the structured log, so that I can investigate incidents.

## Implementation Decisions

### Modules

- **New backend feature `account-security`** (vertical slice under `features/`, scaffolded with `agentGenCli new backend-feature`). It owns TOTP authenticators, recovery codes, passkeys, authentication challenges and the second-factor attempt counter. The Users feature keeps owning credentials and the password login.
- The feature follows the codebase's CQRS shape: query handlers plus small internal static helpers, no service interfaces (as built in mfa-01).
- **`TotpAlgorithm`**: generate secret, build `otpauth://` URI, verify code against a given time with ±1 step window, report the matched time step. Fixed parameters: SHA1, 6 digits, 30 seconds. These are not configurable — Google Authenticator ignores `algorithm`/`digits`/`period`, and Microsoft Authenticator only works reliably with SHA1. In-house RFC 6238 implementation (no Otp.NET dependency), checked against the RFC 6238 Appendix B vectors. Takes time from the existing `ISystemClock`. **`TotpCodes`** adds single use via `LastUsedTimeStep`.
- **`IPasskeyService`** wraps Fido2NetLib (`Fido2` 4.3.x, MIT, net10.0): create registration options, verify attestation, create assertion options, verify assertion. ASP.NET Core Identity's built-in .NET 10 passkey support is not usable because OpenGitBase has a custom user store without `UserManager`.
- **`AuthenticationChallenges`**: single-use challenges shared by both API replicas (see below).
- **`BeginTwoFactorSignInQuery`** (the second-factor gate): decides after a successful primary sign-in (password, Google, Apple) whether to issue a session or a pending-2FA challenge. Every primary sign-in path calls it, so no path can bypass 2FA.
- **`SecondFactorAttempts`**: the per-account failure counter and lockout.
- **`HasRecentReauthenticationQuery`** check for security-sensitive account changes (see *Re-authentication*).

### Schema (EF Core migration in `common/OpenGitBase.Common/Migrations/`)

- `UserTotpAuthenticators`: `UserId` (PK/FK), `SecretProtected`, `ConfirmedAt` (null = never confirmed), `PendingSecretProtected`, `LastUsedTimeStep`, `CreatedAt`. One per user. A new setup writes the pending secret and replaces an earlier pending one; it only replaces the confirmed secret when it is confirmed, so the old authenticator keeps working until then.
- `UserRecoveryCodes`: `Id`, `UserId`, `LookupHash`, `CodeHash`, `UsedAt`, `CreatedAt`. 10 codes per set; regenerating deletes the old set.
- `UserPasskeys`: `Id`, `UserId`, `CredentialId` (bytes, unique), `PublicKey` (COSE bytes), `SignCount`, `Aaguid`, `Transports`, `IsBackupEligible`, `IsBackedUp`, `Name`, `CreatedAt`, `LastUsedAt`.
- `AuthenticationChallenges`: `Id`, `Purpose` (`TwoFactorSignIn`, `PasskeyRegistration`, `PasskeySignIn`, `PasskeySecondFactor`, `ReAuthentication`), `UserId` (nullable for usernameless passkey sign-in), `TokenHash` or `Challenge`, `ExpiresAt`, `CreatedAt`. No per-challenge attempt counter (see *Attempt limits*).
- `UserCredentials` gains `TwoFactorEnabledAt`, `SecondFactorFailedAttempts`, `SecondFactorLockedUntil`.

### Secrets at rest

- The TOTP secret is encrypted with `EncryptSecret` (AES-256-GCM, byte-for-byte). **Never `EncryptEmail`**, which lowercases and trims — the same mistake corrupted the fleet dispatcher SSH key.
- `UserTotpAuthenticators.SecretProtected` and `PendingSecretProtected` are added to the hand-maintained column list in `EncryptionKeyRotation`, with a test.
- Recovery codes are hashed like personal access tokens (SHA-256 lookup hash plus `PasswordHasher` hash).
- Passkey public keys are not secret and are stored in plain form; they need no rotation entry.

### Sign-in contract

- `POST /signin/login` (and the Google/Apple sign-in endpoints) keep returning the session token string and cookie when 2FA is off, unchanged. When 2FA is on they return `200 { "twoFactorRequired": true, "challengeToken": "…", "methods": ["totp","passkey","recovery"] }` and set no cookie. Clients tell the two apart by the `twoFactorRequired` field (web `useAuth`, `cli/auth.vue`, MSW mocks); consumers that never meet 2FA, such as the API test base and E2E `TestIsolation`, are unaffected.
- `challengeToken` is a random opaque value whose SHA-256 is stored in `AuthenticationChallenges` (purpose `TwoFactorSignIn`, 5-minute lifetime). It is not a JWT, so bearer authentication never accepts it.
- `POST /signin/two-factor` with `{ challengeToken, code }`, `{ challengeToken, recoveryCode }`, or `{ challengeToken, passkeyAssertion }` (after `POST /signin/two-factor/passkey-options`). On success the challenge is consumed and the normal session token and cookie are issued. Errors: `401 { error: "invalid_code" | "challenge_expired" }`, `429 { error: "locked_out", lockedUntil }`.
- Attempt limits:
  - One per-account counter (`SecondFactorFailedAttempts`) counts wrong second-factor attempts across challenges and re-authentications, so signing in again does not reset it.
  - The 5th consecutive failure sets `SecondFactorLockedUntil` to 15 minutes from now, returns `429`, consumes the current challenge (the user starts over with the password), and emails the user (email from mfa-06). The counter resets on success.
  - All sign-in endpoints use the `sensitive` rate limiter; Google/Apple sign-in currently lack it and get it.
- Password reset by email does not disable 2FA.
- Admin sign-ins follow the same rules; the `role=admin` claim is only issued once the second step succeeds.

### Passkey endpoints and configuration

- Registration (signed in, re-authenticated): `POST /account/passkeys/registration-options` → `POST /account/passkeys`. Management: `GET /account/passkeys`, `PATCH /account/passkeys/{id}` (rename), `DELETE /account/passkeys/{id}`.
- Sign-in: `POST /signin/passkey/options` → `POST /signin/passkey`. Uses discoverable credentials (usernameless), so the endpoint never reveals whether a username exists. User verification is `required`; a successful passkey sign-in satisfies 2FA.
- As a second factor, user verification is `preferred` (a security-key touch is enough after the password).
- Attestation conveyance `none`; no FIDO Metadata Service.
- The WebAuthn user handle is the user's GUID bytes (no PII).
- Configuration section `WebAuthn`:
  - `RelyingPartyId`: `opengitbase.com` in production. It covers `www.` and `api.` subdomains.
  - `RelyingPartyName`: `OpenGitBase`.
  - `Origins`: the exact allowed origins.
  - Passkeys are disabled (endpoints return `404`, UI hidden) while `RelyingPartyId` is empty. Local compose sets `localhost`, which WebAuthn allows over plain HTTP.
- The relying-party ID is permanent for an installation: changing the domain invalidates every registered passkey. This goes in the operations docs.
- WebAuthn does not work when the site is opened by IP address.

### Challenges across replicas

Two API replicas sit behind round-robin HAProxy, so `IMemoryCache` cannot hold challenges (the existing provider-registration cache already has this flaw). Challenges live in the `AuthenticationChallenges` table and are consumed with a conditional delete that must affect exactly one row. That makes them single-use even under concurrent requests and keeps SQLite-backed tests working. Expired rows are purged opportunistically.

Synced passkeys often report a `SignCount` of 0, so single-use challenges — not the sign counter — are what prevents replay. The counter is stored and checked only when the authenticator increments it.

### Account endpoints

- `GET /account/security` returns `twoFactorAvailable` (the rollout switch), `twoFactorEnabled`, `totpConfigured`, `hasPassword`, and later the passkeys and the unused recovery-code count. `/account/me` gains `twoFactorEnabled`.
- TOTP:
  - `POST /account/two-factor/totp/setup` returns `{ otpauthUri, secret }` and stores the pending secret.
  - `POST /account/two-factor/totp/confirm` with `{ code }`. The first confirmation sets `TwoFactorEnabledAt` and returns 10 recovery codes once.
  - `DELETE /account/two-factor/totp` is allowed only if another second factor remains or 2FA is being disabled. It arrives with the last-factor rule in mfa-05; until then the authenticator is removed by disabling 2FA.
- `POST /account/two-factor/enable` (requires at least one passkey) turns 2FA on without an authenticator app and returns recovery codes.
- `POST /account/two-factor/disable` takes no body. Like every sensitive change it needs a recent re-authentication of the session, which already asks for the password and a second factor. It removes the TOTP authenticator and recovery codes and clears `TwoFactorEnabledAt`. Passkeys stay as sign-in methods.
- `POST /account/two-factor/recovery-codes` regenerates the set.
- Deleting the last second factor while 2FA is on returns `409`.
- Turning 2FA on or off sets `SessionsValidAfter` and re-issues the caller's token, reusing the change-password pattern.
- otpauth label: `{issuer}:{username}` with `issuer={issuer}`. The issuer comes from configuration (`TwoFactor:Issuer`, default `OpenGitBase`) so self-hosted instances show their own name.

### Re-authentication

Turning 2FA on or off, adding or removing a passkey, setting up or removing TOTP, and regenerating recovery codes require a recent confirmation. Any of these counts:

- the current password (and, with 2FA on, a second factor) via `POST /account/reauthenticate`;
- a passkey assertion within the last 10 minutes;
- for accounts without a password and without passkeys (Google/Apple-only), a session issued within the last 10 minutes (`ogb_iat_ms` claim).

A sensitive endpoint without a recent confirmation answers `403 { error: "reauthentication_required" }`; the web app then asks for it and repeats the action. The confirmation is recorded as a 10-minute `ReAuthentication` challenge bound to the user and the session's `jti`. This prevents someone holding a stolen session from enabling 2FA on the victim's account and locking them out.

### Notifications and logging

- Emails via the existing SendGrid service: 2FA turned on/off, passkey added, recovery code used, second-factor lockout, admin reset.
- The same events are logged as structured `ILogger` events with user id and remote IP. A persistent audit-log table is out of scope.

### Administration

`POST /admin/users/{username}/two-factor/reset` (`role=admin`):
- deletes the TOTP authenticator, recovery codes and all passkeys;
- clears `TwoFactorEnabledAt`;
- revokes all sessions and emails the user.

There is no admin user management UI yet, so v1 exposes the API only; the operations docs describe how to call it.

### Rollout switch

`TwoFactor:Enabled` gates **turning 2FA on**, following the `Lfs:Enabled` pattern.

- Default `false`; local compose and tests set `true`.
- While it is off:
  - TOTP setup/confirm and passkey-only enable return `404`;
  - the settings page hides the 2FA section.
- Disabling 2FA, regenerating recovery codes and the second sign-in step stay available. An account that already has 2FA on is never weakened by switching it off.
- Production switches it on only once recovery codes, notifications and admin reset have shipped. Until then no account can lock itself out without a recovery path.
- Passkeys as a sign-in method have their own gate (`WebAuthn:RelyingPartyId`).

### Web UI

- `sign-in.vue`:
  - "Sign in with a passkey" button.
  - Conditional mediation on the username field (`autocomplete="username webauthn"`).
  - Second-step view with a 6-digit code field (`autocomplete="one-time-code"`, `inputmode="numeric"`) and switches to "Use a passkey" and "Use a recovery code".
- New `settings/security.vue`:
  - 2FA status.
  - Authenticator setup dialog with QR code and setup key.
  - Recovery codes (show once, copy, download as `.txt`, regenerate).
  - Passkey list (add, rename, delete).
  - Re-authentication dialog.
- QR codes are generated in the browser with a bundled library. The secret is never sent to a third-party QR service.
- The browser API (`navigator.credentials` with `PublicKeyCredential.parseCreationOptionsFromJSON` / `parseRequestOptionsFromJSON`, Baseline 2025) is used directly. A small base64url fallback covers older browsers.
- `cli/auth.vue` gets the same second step and passkey button before redirecting to the loopback callback.
- i18n keys are added under `auth` and `settings`.

### CLI

`ogb auth login` raises its loopback timeout from 30 s to 5 minutes. Nothing else changes; the CLI still receives a normal session token.

## Testing Decisions

Tests cover external behavior at the lowest layer that proves it, per the engineering contract.

- **TOTP service (unit):**
  - RFC 6238 Appendix B SHA1 test vectors.
  - ±1 step window; rejection outside the window; matched time step reported.
  - otpauth URI format (label, issuer, Base32 secret without padding).
  - Fixed clock via `ISystemClock`.
- **Account-security handlers (feature tests, SQLite fixture as in `OpenGitBase.Features.Users.Tests`):**
  - Setup/confirm/replace TOTP; replay rejection via `LastUsedTimeStep`.
  - Recovery code generation, single use, regeneration invalidating the old set.
  - Challenge single-use under a concurrent double-consume.
  - Lockout counter across challenges and re-authentications (the 5th failure ends the challenge), and its reset.
  - Refusing removal of the last second factor.
  - Every handler gets a `{Name}QueryHandlerTests` class, as the meta-tests require.
- **Passkeys:**
  - A test-only software authenticator (ES256 key pair, attestation `none`) that produces `clientDataJSON`, `authenticatorData` and signatures. It drives registration and assertion through the real Fido2NetLib verification.
  - Covers: wrong origin, wrong RP ID, reused challenge and missing user verification are rejected.
  - No prior art exists in the codebase; this helper is new.
- **API (`OpenGitBase.Api.Tests`):**
  - Two-step sign-in for password, Google and Apple paths (fake validators already exist).
  - The pending challenge token is rejected as a bearer token.
  - No cookie before the second step; admin claim only after it.
  - Passkey sign-in issues a session without a second step.
  - Re-authentication enforcement on every sensitive endpoint.
  - Sessions revoked on enable/disable; admin reset.
  - `EncryptionKeyRotation` re-protects TOTP secrets.
  - Controller test classes per the meta-tests. Test base helpers (`ControllerTestBase`, `/signin/testlogin`) stay as they are, since the login answer without 2FA did not change.
- **CLI:** the login timeout value; handler tests stay green with the new web flow (the CLI contract is unchanged).
- **Web:**
  - Unit tests for the sign-in state machine (password → second step → session) and the WebAuthn JSON helpers.
  - MSW handlers for the new endpoints.
  - Playwright visual snapshots for the sign-in second step, passkey button, security settings page, authenticator setup dialog and recovery-code display.
  - A Playwright test using Chromium's virtual WebAuthn authenticator (CDP `WebAuthn.addVirtualAuthenticator`) for passkey registration and sign-in.
- **Compose E2E:**
  - One end-to-end TOTP round trip: enable, sign out, sign in with a computed code. The test computes codes with the same RFC 6238 logic.
  - `TestIsolation` login helpers unchanged (accounts there have no 2FA).
  - Migration applied via the normal compose path.

## Out of Scope

- Mandatory 2FA (per instance, per organization, or for admins). The schema leaves room for a later policy.
- SMS codes, email codes, push approval. Push in Microsoft Authenticator only works for Microsoft Entra accounts.
- "Remember this device" / trusted devices.
- Passwordless accounts (removing the password once a passkey exists).
- Step-up prompts before creating personal access tokens or SSH keys.
- A persistent audit-log table and an admin user-management UI.
- Attestation enforcement, FIDO Metadata Service, allow-lists of authenticator models.
- WebAuthn related origins (multiple registrable domains for one RP).
- Revoking an admin role mid-session (an existing JWT-claim limitation, unrelated to 2FA).

## Further Notes

- **Which phone apps work:** Google Authenticator and Microsoft Authenticator act as TOTP apps for OpenGitBase. Neither stores passkeys for OpenGitBase: Microsoft Authenticator holds passkeys only for Microsoft Entra ID accounts. Passkeys live in the platform credential managers (iCloud Keychain, Google Password Manager, Windows Hello), in password managers (1Password, Bitwarden), or on hardware keys.
- **Clock sync:** TOTP depends on accurate server time; API hosts must run NTP.
- **Suggested slice order:**
  1. Two-step sign-in infrastructure + TOTP
  2. Recovery codes
  3. Passkey registration and management
  4. Passkey sign-in (button + conditional UI)
  5. Passkey as second factor and passkey-only 2FA
  6. Notifications, lockout email and admin reset

  Re-authentication lands with slice 1 because TOTP setup already needs it.
- **Existing gaps noticed while mapping the auth surface:**
  - Google/Apple sign-in endpoints are anonymous and not rate-limited.
  - Provider registration state lives in per-replica `IMemoryCache`.
  - Sign-out does not revoke the token server-side.

  The first is fixed here; the other two are left for separate work.
- References:
  - [Google Authenticator Key URI Format](https://github.com/google/google-authenticator/wiki/Key-Uri-Format)
  - [RFC 6238](https://www.rfc-editor.org/rfc/rfc6238)
  - [fido2-net-lib](https://github.com/passwordless-lib/fido2-net-lib)
  - [Otp.NET](https://github.com/kspearrin/Otp.NET)
  - [MDN: parseCreationOptionsFromJSON](https://developer.mozilla.org/en-US/docs/Web/API/PublicKeyCredential/parseCreationOptionsFromJSON_static)
  - [Microsoft Entra passkey/FIDO2 compatibility](https://learn.microsoft.com/en-us/entra/identity/authentication/fido2-compatibility)
