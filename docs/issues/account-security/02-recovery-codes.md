<!-- forge: #246 -->

# [slice] mfa-02 — Recovery codes

## Metadata

- Type: AFK
- Status: ready

## Parent

PRD discussion #244

## What to build

One-time recovery codes, so that a lost phone does not lock anyone out. This is the precondition for ever turning `TwoFactor:Enabled` on in production.

1. **Backend.**
   - Migration: new table `UserRecoveryCodes` with `Id`, `UserId`, `LookupHash`, `CodeHash`, `UsedAt`, `CreatedAt`.
   - **Code format:** 10 codes per set, each 10 random Base32 characters, displayed as `xxxxx-xxxxx`.
   - **Hashing** like personal access tokens: SHA-256 lookup hash plus `PasswordHasher` hash. Input is normalized before hashing: trimmed, dash and spaces removed, case-insensitive.
   - **Issuing:** the first TOTP confirmation from mfa-01 now also returns `recoveryCodes`, once only.
   - **`POST /signin/two-factor`** also accepts `{ challengeToken, recoveryCode }`:
     - A used code gets `UsedAt` and is never accepted again.
     - Wrong codes count toward the same challenge and account limits as wrong TOTP codes.
     - `methods` includes `recovery` while the user has unused codes.
   - **`POST /account/two-factor/recovery-codes`** (re-authenticated) deletes the old set and returns a new set, once only.
   - `GET /account/security` reports the number of unused codes.
   - Disabling 2FA deletes all codes.
   - Structured log event when a recovery code is used. The email for this comes in mfa-06.
2. **Web.**
   - **After 2FA is turned on:** a recovery-codes panel with copy, a `.txt` download, and an "I have stored these codes" confirmation before it closes.
   - **Security page:** the remaining count, plus "Generate new codes", which warns that the old codes stop working and shows the new set once.
   - **Second sign-in step:** a "Use a recovery code" switch, in `sign-in.vue` and in `cli/auth.vue`.
   - A warning when 2 or fewer codes are left.

## Acceptance criteria

- [ ] The first TOTP confirmation returns 10 codes; later calls never return them again
- [ ] Only hashes are stored; the plaintext is not recoverable from the database (test)
- [ ] A recovery code completes the second step exactly once; reusing it is rejected
- [ ] Input with or without the dash, in any case and with spaces is accepted
- [ ] Wrong recovery codes count toward the challenge and account lockout limits
- [ ] Regeneration requires re-authentication and invalidates every code of the old set
- [ ] Disabling 2FA deletes all codes; the security endpoint reports the correct unused count
- [ ] Web: codes panel with copy and download; recovery option in the sign-in second step and in CLI sign-in
- [ ] Playwright visual snapshots: recovery-codes panel, sign-in step with recovery code
- [ ] Controller and query-handler tests satisfy the coverage meta-tests

## Blocked by

- #245 (mfa-01)

## User stories covered

- 12, 22, 23, 24, 25, 26, 28
