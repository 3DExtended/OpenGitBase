<!-- forge: #247 -->

# [slice] mfa-03 — Passkey registration and management

## Metadata

- Type: AFK
- Status: ready

## Parent

PRD discussion #244

## What to build

Signed-in users register, name, list and delete passkeys. The WebAuthn assertion verification built here is used for re-authentication now and reused by mfa-04 (sign-in) and mfa-05 (second factor).

1. **Backend.**
   - **`IPasskeyService`** wraps Fido2NetLib (`Fido2` 4.3.x, MIT). ASP.NET Identity's built-in passkeys need `UserManager`, which OpenGitBase does not have.
   - **Configuration** `WebAuthn:RelyingPartyId`, `WebAuthn:RelyingPartyName`, `WebAuthn:Origins`:
     - An empty RP ID disables passkeys: endpoints return `404` and the UI is hidden.
     - Local compose sets `localhost` plus its origins (WebAuthn allows plain HTTP on localhost).
     - Production values are set in mfa-06.
   - **Migration:** new table `UserPasskeys` with `Id`, `UserId`, `CredentialId` (unique), `PublicKey`, `SignCount`, `Aaguid`, `Transports`, `IsBackupEligible`, `IsBackedUp`, `Name`, `CreatedAt`, `LastUsedAt`. Public keys are not secret, so there is no encryption and no rotation entry.
   - **`POST /account/passkeys/registration-options`** (re-authenticated) returns creation options with:
     - resident key `required` (needed for usernameless sign-in in mfa-04);
     - user verification `required`;
     - attestation `none`;
     - `excludeCredentials` set to the user's existing passkeys;
     - user handle = the user's GUID bytes.

     The challenge is stored as a single-use `PasskeyRegistration` challenge with a 5-minute lifetime.
   - **`POST /account/passkeys`** with `{ name, attestationResponse }`:
     - Verifies origin, RP ID hash, challenge and the user-verification flag.
     - Stores the credential; a duplicate credential id is rejected.
   - **Management:** `GET /account/passkeys`, `PATCH /account/passkeys/{id}` (rename), `DELETE /account/passkeys/{id}` (re-authenticated). Another user's passkey returns `404`.
   - **Re-authentication by passkey:** `POST /account/reauthenticate/passkey-options`, then `POST /account/reauthenticate` with an assertion for one of the user's own passkeys.
   - Google/Apple-only users can add passkeys; their re-authentication is a recent session, as in mfa-01.
   - Structured log events: passkey added, passkey removed.
2. **Web.**
   - **Passkeys section on the security page:**
     - The list shows name, created, last used and a "synced" badge when backed up.
     - Add asks for a name; rename works inline; delete asks for confirmation.
   - The re-authentication dialog offers "Use a passkey".
   - **WebAuthn helpers:**
     - Use `PublicKeyCredential.parseCreationOptionsFromJSON` / `parseRequestOptionsFromJSON`, with a small base64url fallback.
     - Feature-detect `window.PublicKeyCredential` and hide the section when it is missing.
3. **Test tooling** (new, no prior art in the repo).
   - A software authenticator for API tests: an ES256 key pair with attestation `none`, producing `clientDataJSON`, `authenticatorData` and signatures, run through the real Fido2NetLib verification.
   - A Playwright test using Chromium's virtual authenticator (CDP `WebAuthn.addVirtualAuthenticator`).

## Acceptance criteria

- [ ] With an empty `WebAuthn:RelyingPartyId`, the passkey endpoints return `404` and the UI hides passkeys
- [ ] Registration with the software authenticator succeeds and stores the credential; the options exclude existing credentials
- [ ] Registration is rejected for a wrong origin, a wrong RP ID, a reused or expired challenge, and missing user verification
- [ ] Registration and delete require re-authentication; rename only needs a session
- [ ] A user cannot list, rename or delete another user's passkeys
- [ ] Re-authentication with a passkey assertion unlocks sensitive actions; an assertion for another user's credential is rejected
- [ ] A Google/Apple-only account can register a passkey
- [ ] Playwright: register, rename and delete a passkey with the virtual authenticator; visual snapshot of the passkeys section
- [ ] Controller and query-handler tests satisfy the coverage meta-tests

## Blocked by

- #245 (mfa-01)

## User stories covered

- 29, 30, 31, 32, 33, 38, 40, 41, 46
