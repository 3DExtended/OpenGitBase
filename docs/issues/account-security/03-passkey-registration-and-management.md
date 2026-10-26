<!-- forge: #247 -->

# [slice] mfa-03 — Passkey registration and management

## Metadata

- Type: AFK
- Status: done

## Parent

PRD discussion #244

## What to build

Signed-in users register, name, list and delete passkeys. The WebAuthn assertion verification built here is used for re-authentication now and reused by mfa-04 (sign-in) and mfa-05 (second factor).

1. **Backend.**
   - **`PasskeyCeremonies`** (internal static helpers, see Deviations) wraps Fido2NetLib (`Fido2` 4.3.1, MIT). ASP.NET Identity's built-in passkeys need `UserManager`, which OpenGitBase does not have.
   - **Configuration** `WebAuthn:RelyingPartyId`, `WebAuthn:RelyingPartyName`, `WebAuthn:Origins`:
     - An empty RP ID disables passkeys: endpoints return `404` and the UI is hidden.
     - `docker-compose.e2e.yml` and `docker-compose.override.example.yml` set `localhost` plus their origins (WebAuthn allows plain HTTP on localhost). `docker-compose.yml` leaves it empty, so production stays off.
     - Production values are set in mfa-06.
   - **Migration `AddPasskeys`:**
     - New table `UserPasskeys` with `Id`, `UserId` (→ `Users`, cascade), `CredentialId` (unique), `PublicKey`, `SignCount` (`bigint`), `Aaguid`, `Transports`, `IsBackupEligible`, `IsBackedUp`, `Name`, `CreatedAt`, `LastUsedAt`. Public keys are not secret, so there is no encryption and no rotation entry.
     - `AuthenticationChallenges` gains `Payload`, the options JSON of an open WebAuthn ceremony.
   - **`POST /account/passkeys/registration-options`** (re-authenticated) returns creation options with:
     - resident key `required` (needed for usernameless sign-in in mfa-04);
     - user verification `required`;
     - attestation `none`;
     - `excludeCredentials` set to the user's existing passkeys;
     - user handle = the user's GUID bytes.

     The challenge is stored as a single-use `PasskeyRegistration` challenge with a 5-minute lifetime, bound to the user. Its `TokenHash` is the SHA-256 of the challenge, so the answer finds it again through `clientDataJSON`.
   - **`POST /account/passkeys`** with `{ name, attestationResponse }` (re-authenticated):
     - The name is 1 to 64 characters after trimming, otherwise `400 invalid_name`.
     - Verifies origin, RP ID hash, challenge and the user-verification flag; any failure is `400 passkey_rejected`, a used or expired challenge `400 challenge_expired`.
     - Stores the credential (`201`); a duplicate credential id is `409 passkey_already_registered`.
   - **Management:** `GET /account/passkeys`, `PATCH /account/passkeys/{id}` (rename, session only), `DELETE /account/passkeys/{id}` (re-authenticated). Another user's passkey returns `404`.
   - **Re-authentication by passkey:** `POST /account/reauthenticate/passkey-options` (`400 no_passkeys` without one), then `POST /account/reauthenticate` with `{ passkeyAssertion }` for one of the user's own passkeys:
     - Success is `204` and records the usual 10-minute re-authentication for this session. A user-verified passkey replaces password and code.
     - Another user's credential, a missing UV flag or a bad signature is `401 passkey_rejected`; a reused or expired challenge is `401 challenge_expired`.
     - The challenge uses its own purpose, `PasskeyReauthentication`, so a registration challenge cannot be answered as a re-authentication.
     - Each success updates `SignCount`, `IsBackedUp` and `LastUsedAt`.
   - Google/Apple-only users can add passkeys; their re-authentication is a recent session, as in mfa-01, or now a passkey.
   - `GET /account/security` reports `passkeysAvailable`.
   - Structured log events: passkey added, passkey removed, and rejected passkey ceremonies with the reason.
2. **Web.**
   - **Passkeys card on the security page:**
     - The list shows name, created, last used and a "Synced" badge when backed up.
     - Add asks for a name; rename works inline; delete asks for confirmation.
     - When the API asks for re-authentication, the dialog opens and the action runs again afterwards, as for 2FA.
   - The re-authentication dialog offers "Use a passkey" while the account has one. Accounts without a password and without 2FA see only that button.
   - **WebAuthn helpers** (`utils/webauthn.ts`):
     - Use `PublicKeyCredential.parseCreationOptionsFromJSON` / `parseRequestOptionsFromJSON` and `credential.toJSON()`, with a small base64url fallback for each.
     - Feature-detect `window.PublicKeyCredential` and hide the card when it is missing.
     - Cancelled prompts (`NotAllowedError`) and authenticators that already hold the passkey (`InvalidStateError`) get their own messages.
3. **Test tooling** (new, no prior art in the repo).
   - `SoftwarePasskeyAuthenticator` (`OpenGitBase.Common.Tests/Testing`, also compiled into `OpenGitBase.E2E.Core`): an ES256 key pair with attestation `none`, producing `clientDataJSON`, `authenticatorData` and signatures the way `PublicKeyCredential.toJSON()` writes them. They run through the real Fido2NetLib verification. Settable origin, RP ID, UV flag and synced flag produce the answers the server must reject.
   - A Playwright test (`tests/visual/passkeys.spec.ts`) using Chromium's virtual authenticator (CDP `WebAuthn.addVirtualAuthenticator`): register with password re-authentication, rename, then delete after re-authenticating with the new passkey. A stand-in API checks challenge, origin and ceremony type of what Chromium sends, and the authenticator holds a resident credential.
   - Compose tier 1 `PasskeyE2eTests`: register, re-authenticate a later session with the passkey, reject the replayed assertion, and delete, against Postgres and both replicas.

## Deviations from the original slice

- **No `IPasskeyService`.** The feature uses internal static helpers (`PasskeyCeremonies`) called by the query handlers, like `TotpCodes` and `RecoveryCodes` in mfa-01 and mfa-02. The PRD already says "no service interfaces".
- **The ceremony options live in `AuthenticationChallenges.Payload`.** Fido2NetLib verifies an answer against the original options, and the two replicas cannot share memory. The challenge row stores them; `TokenHash` is the SHA-256 of the WebAuthn challenge.
- **Separate purpose `PasskeyReauthentication`** next to `PasskeyRegistration`, so one ceremony's challenge cannot be answered as the other.
- **`SignCount` is a `bigint`.** Npgsql maps `uint` to `xid`; the counter is a 32-bit unsigned value, so `long` holds it without loss.
- **Passwordless accounts keep the recent-session rule after adding a passkey.** The PRD scoped it to accounts "without a password and without passkeys". Such an account just proved itself to Google or Apple, which is no weaker than mfa-01; it can now also confirm with a passkey.

## Acceptance criteria

- [x] With an empty `WebAuthn:RelyingPartyId`, the passkey endpoints return `404` and the UI hides passkeys
- [x] Registration with the software authenticator succeeds and stores the credential; the options exclude existing credentials
- [x] Registration is rejected for a wrong origin, a wrong RP ID, a reused or expired challenge, and missing user verification
- [x] Registration and delete require re-authentication; rename only needs a session
- [x] A user cannot list, rename or delete another user's passkeys
- [x] Re-authentication with a passkey assertion unlocks sensitive actions; an assertion for another user's credential is rejected
- [x] A Google/Apple-only account can register a passkey
- [x] Playwright: register, rename and delete a passkey with the virtual authenticator; visual snapshot of the passkeys section
- [x] Controller and query-handler tests satisfy the coverage meta-tests

## Blocked by

- #245 (mfa-01)

## User stories covered

- 29, 30, 31, 32, 33, 38, 40, 41, 46
