<!-- forge: #246 -->

# [slice] mfa-02 — Recovery codes

## Metadata

- Type: AFK
- Status: done

## Parent

PRD discussion #244

## What to build

One-time recovery codes, so that a lost phone does not lock anyone out. This is the precondition for ever turning `TwoFactor:Enabled` on in production.

1. **Backend.**
   - Migration `AddRecoveryCodes`: new table `UserRecoveryCodes` with `Id`, `UserId`, `CodeHash`, `UsedAt`, `CreatedAt`.
   - **Code format:** 10 codes per set, each 10 random Base32 characters, displayed as `xxxxx-xxxxx`.
   - **Hashing:** `PasswordHasher` hash only (no SHA-256 lookup hash, see Deviations). Input is normalized before hashing and checking: dash and spaces removed, case-insensitive.
   - **Issuing:** the first TOTP confirmation from mfa-01 now also returns `recoveryCodes`, once only. Replacing the app later returns none and keeps the set.
   - **`POST /signin/two-factor`** accepts exactly one of `{ challengeToken, code }` and `{ challengeToken, recoveryCode }` (both or neither is `400`):
     - A used code gets `UsedAt` through a conditional update and is never accepted again.
     - Wrong codes count toward the same account lockout as wrong TOTP codes (5 → 15 minutes, which also ends the challenge).
     - `methods` includes `recovery` while the user has unused codes.
   - **`POST /account/reauthenticate`** also accepts `recoveryCode` instead of `code`.
   - **`POST /account/two-factor/recovery-codes`** (re-authenticated) deletes the old set, used or not, and returns a new set, once only. `400 two_factor_not_enabled` without 2FA. Stays available while the switch is off.
   - `GET /account/security` reports `recoveryCodesRemaining` (unused codes).
   - Disabling 2FA deletes all codes.
   - Structured log event when a recovery code is used (step and remaining count) and when the set is regenerated. The email for this comes in mfa-06.
2. **Web.**
   - **After 2FA is turned on:** a recovery-codes panel with copy, a `.txt` download, and an "I have stored these codes" confirmation before it closes.
   - **Security page:** the remaining count, plus "Generate new codes", which warns that the old codes stop working and shows the new set once.
   - **Second sign-in step:** a "Use a recovery code" switch, in `sign-in.vue` and in `cli/auth.vue`, shown while the challenge offers `recovery`. Also in the re-authentication dialog.
   - A warning when 2 or fewer codes are left.

## Deviations from the original slice

- **No SHA-256 lookup hash.** A personal access token carries 256 random bits, so an unsalted SHA-256 lookup hash of it is safe. A recovery code has 50 bits; an unsalted SHA-256 of it can be brute-forced from a database leak, which defeats the `PasswordHasher` hash next to it. The handler checks the input against the user's at most 10 unused hashes instead, which is cheap because the challenge already identifies the user.
- **Re-authentication accepts a recovery code.** Someone who lost the phone signs in with a recovery code and then has to set up a new phone, regenerate codes or turn 2FA off. All three need a re-authentication, which would otherwise ask for the code from the lost phone.
- **Regeneration stays available with the switch off,** like disabling, so accounts that already have 2FA keep a way back in.

## Acceptance criteria

- [x] The first TOTP confirmation returns 10 codes; later calls never return them again
- [x] Only hashes are stored; the plaintext is not recoverable from the database (test)
- [x] A recovery code completes the second step exactly once; reusing it is rejected
- [x] Input with or without the dash, in any case and with spaces is accepted
- [x] Wrong recovery codes count toward the challenge and account lockout limits
- [x] Regeneration requires re-authentication and invalidates every code of the old set
- [x] Disabling 2FA deletes all codes; the security endpoint reports the correct unused count
- [x] Web: codes panel with copy and download; recovery option in the sign-in second step and in CLI sign-in
- [x] Playwright visual snapshots: recovery-codes panel, sign-in step with recovery code
- [x] Controller and query-handler tests satisfy the coverage meta-tests

## Blocked by

- #245 (mfa-01)

## User stories covered

- 12, 22, 23, 24, 25, 26, 28
