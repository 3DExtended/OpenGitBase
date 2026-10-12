# Production deployment secrets

Use the production Compose overlay so the API starts in `Production`, debug bypasses stay off, and Postgres/Redis are not published to the host:

```bash
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --build
```

The API refuses to start in `Production` when known dev placeholder secrets are still configured. Override every value below before exposing the stack.

## Required secret overrides

| Setting | Environment variable | Notes |
|---------|---------------------|-------|
| JWT signing key | `Jwt__Key` | At least 32 bytes; must not use `dev-` prefixes or committed defaults |
| Email encryption data key | `Encryption__DataKey` | Base64-encoded 32-byte AES-256 key; not the all-`A` placeholder |
| Email lookup pepper | `Encryption__Pepper` | Not `dev-pepper-change-me` |
| Platform merge token | `PlatformMergeIdentity__AccessToken` | Shared secret for platform merge identity calls |
| Database password | `Sql__ConnectionString` | Not the compose default `postgres` / `postgres` |
| Admin seed password | `AdminSeed__Password` | Not `change-me-admin`; disable seeding with `AdminSeed__Enabled=false` when using external identity |
| SendGrid | `SendGrid__ApiKey`, `SendGrid__FromEmailAddress` | Required when outbound email is enabled |

## Must remain disabled in production

| Setting | Environment variable | Expected value |
|---------|---------------------|----------------|
| Debug email verification bypass | `Debug__Features__EmailVerification` | `false` |
| E2E email capture / reset endpoints | `E2E__CaptureEmail` | unset or `false` |
| ASP.NET Core environment | `ASPNETCORE_ENVIRONMENT` | `Production` |

`docker-compose.prod.yml` sets `ASPNETCORE_ENVIRONMENT=Production`, turns off the debug email-verification flag, and removes host port bindings for Postgres and Redis. It does **not** set the cryptographic secrets above — provide them via a private `.env` file or `docker-compose.override.yml` that is not committed.

## Rotating the encryption keys

`Encryption__DataKey` wraps every repository key, the storage node API tokens, the dispatcher SSH private key, and the stored email addresses. Losing the data key makes encrypted repositories unreadable, so keep a copy outside the host (for example in a password manager) before and after a rotation. Never simply replace the key: existing rows would no longer decrypt.

1. Back up the database (`pg_dump`).
2. On `api-1` and `api-2`, set the new `Encryption__DataKey` and `Encryption__Pepper` and keep the old data key as `Encryption__PreviousDataKeys__0`. Recreate both API instances one at a time. They now read values written with either key and write with the new one.
3. Re-encrypt everything once:

   ```bash
   docker compose run --rm --no-deps -e OPENGITBASE_ROTATE_ENCRYPTION_KEYS=true api-1
   ```

   The run re-encrypts all protected columns with the new data key and recomputes the email lookup hashes with the new pepper, in one transaction. If any value cannot be decrypted, it changes nothing and exits with code 1. Email lookups (duplicate-email check, organization invites) can miss between step 2 and this step, so run it right after the recreate.
4. Once the run has succeeded, `Encryption__PreviousDataKeys__0` can be removed.

## E2E endpoints

`/internal/e2e/*` stays disabled unless **both** of the following are true:

1. The host environment is `Development` or `E2ETest`
2. `E2E:CaptureEmail` (or `E2E__CaptureEmail`) is explicitly `true`

The default production compose profile does not enable E2E capture, so database reset and captured-email endpoints are unreachable.

## Example private override

```yaml
# docker-compose.override.yml (untracked)
services:
  api-1:
    environment:
      Jwt__Key: "${JWT_SIGNING_KEY}"
      Encryption__DataKey: "${ENCRYPTION_DATA_KEY}"
      Encryption__Pepper: "${ENCRYPTION_PEPPER}"
      PlatformMergeIdentity__AccessToken: "${PLATFORM_MERGE_TOKEN}"
      Sql__ConnectionString: "Host=postgres;Port=5432;Database=opengitbase;Username=opengitbase;Password=${POSTGRES_PASSWORD}"
      AdminSeed__Enabled: "false"
  api-2:
    environment:
      Jwt__Key: "${JWT_SIGNING_KEY}"
      Encryption__DataKey: "${ENCRYPTION_DATA_KEY}"
      Encryption__Pepper: "${ENCRYPTION_PEPPER}"
      PlatformMergeIdentity__AccessToken: "${PLATFORM_MERGE_TOKEN}"
      Sql__ConnectionString: "Host=postgres;Port=5432;Database=opengitbase;Username=opengitbase;Password=${POSTGRES_PASSWORD}"
      AdminSeed__Enabled: "false"
```
