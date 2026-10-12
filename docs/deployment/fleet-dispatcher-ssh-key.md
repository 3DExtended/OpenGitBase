# Fleet dispatcher SSH key

The API holds one dispatcher→storage SSH key pair in the `FleetSecrets` table. The public key is stored in clear. The private key is AES-GCM encrypted with `EncryptSecret`, byte-for-byte, including the trailing newline that OpenSSH requires after the `END` marker.

## Consumers

| Consumer | What it does | When |
|----------|--------------|------|
| `POST /api/admin/fleet/dispatcher-ssh-keys/generate` (admin) | Replaces the key pair **and** the fleet bootstrap token. Returns the public key and the token; the private key stays in the API | On demand (`scripts/bootstrap-fleet.sh`) |
| Dispatcher [`entrypoint.sh`](../../applications/OpenGitBase.Dispatcher/entrypoint.sh) | Fetches the private key from `GET /api/v1/fleet/bootstrap/dispatcher-ssh-private-key` with `X-Fleet-Bootstrap-Token` and writes it to `/run/secrets/dispatcher_storage_ssh`. Skipped when the file exists. The token works **once** | Container start |
| Dispatcher SSH session (`GitSessionProxyService`) | Uses the key to SSH into a storage node as `git` | SSH git only (`GIT_SSH_ENABLED=true`). Smart HTTP never reads it |
| Storage [`entrypoint.sh`](../../applications/repo-storage-layer/entrypoint.sh) | Writes `DISPATCHER_SSH_PUBLIC_KEY` (or the public key fetched from the API when that is empty) to `/home/git/.ssh/authorized_keys` | Every container start |

The dispatcher key file lives in the container's writable layer (no volume). `docker restart` keeps it; `--force-recreate` drops it and the entrypoint fetches again.

## Keys generated before the normalization fix are unusable

Before the fix, the private key went through `EncryptEmail`, which trims and lower-cases. That always corrupts an OpenSSH key (the `BEGIN OPENSSH PRIVATE KEY` header alone changes), and OpenSSH rejects the result with `invalid format`. So there is no need to inspect key material: a `FleetSecrets` row last updated before the fix was deployed holds a corrupted key.

With `GIT_SSH_ENABLED=false` nothing reads the private key, so the corruption is latent and Smart HTTP is unaffected.

Read-only checks:

```bash
# When was the key pair last generated?
docker exec opengitbase_postgres psql -U postgres -d opengitbase \
  -c 'SELECT "UpdatedAt" FROM "FleetSecrets";'

# Does a dispatcher hold a usable key? Prints valid / corrupt / missing, never the key.
docker exec opengitbase_dispatcher_1 sh -c 'f=/run/secrets/dispatcher_storage_ssh; if [ ! -s "$f" ]; then echo missing; elif ssh-keygen -y -f "$f" >/dev/null 2>&1; then echo valid; else echo corrupt; fi'
```

## Regenerating

Regenerate only after the fixed API runs on **every** API instance. `api-lb` can send the generate call to any of them, and an old instance stores another corrupted key.

With SSH disabled, regenerating is safe: it touches only `FleetSecrets`, and Smart HTTP never reads that table.

Do **not** rerun `scripts/bootstrap-fleet.sh` for this. Besides the key, it mints new storage and compute enrollment tokens and rewrites them into `docker-compose.override.yml`.

1. Rotate the key pair. Keep the response in shell variables, not on disk:

   ```bash
   API_URL=http://localhost:8089   # API load balancer as reachable from the host
   TOKEN=$(curl -fsS -X POST "${API_URL}/api/signin/login" \
     -H "Content-Type: application/json" \
     -d "{\"username\":\"${ADMIN_USER}\",\"password\":\"${ADMIN_PASS}\"}" | tr -d '\n\r"')
   FLEET_JSON=$(curl -fsS -X POST "${API_URL}/api/admin/fleet/dispatcher-ssh-keys/generate" \
     -H "Authorization: Bearer ${TOKEN}")
   echo "${FLEET_JSON}" | python3 -c 'import json,sys; print(json.load(sys.stdin)["dispatcherSshPublicKey"])'
   echo "${FLEET_JSON}" | python3 -c 'import json,sys; print(json.load(sys.stdin)["fleetBootstrapToken"])'
   ```

2. In `docker-compose.override.yml`, set:
   - `DISPATCHER_SSH_PUBLIC_KEY` on `storage-1` … `storage-4` to the new public key
   - `FLEET_BOOTSTRAP_TOKEN` on `dispatcher-1` to the new token

3. Recreate the storage nodes **one at a time** so each rewrites `authorized_keys`. Wait for `(healthy)` before the next:

   ```bash
   docker compose up -d --no-deps --force-recreate storage-1
   ```

4. Recreate `dispatcher-1`. Do not use `restart`, which keeps the old key file and skips the fetch:

   ```bash
   docker compose up -d --no-deps --force-recreate dispatcher-1
   ```

   Then run the dispatcher check above. Expect `valid`. `docker exec opengitbase_dispatcher_1 ssh-keygen -y -f /run/secrets/dispatcher_storage_ssh` must print the new public key (without the comment).

5. `dispatcher-2` cannot fetch the key: the token was used up in step 4. See below.

## Known gaps in the SSH bootstrap path

These came from reading the code while fixing the normalization bug. They are not fixed yet and were not reproduced on a running stack. Fix them before turning on `GIT_SSH_ENABLED=true`. Until then, keep SSH git disabled even after you regenerate the key.

- **One single-use token, two dispatchers.** The API clears the bootstrap token after the first successful fetch, but `bootstrap-fleet.sh` writes the same token to `dispatcher-1` and `dispatcher-2`. Whichever starts second gets `401`.
- **A failed fetch leaves an empty key file.** The `> "${PRIVATE_KEY_PATH}"` redirect creates the file before the fetch fails. After the automatic restart the file exists, so the fetch is skipped and the dispatcher runs with an empty key.
- **The SSH session cannot read the key.** sshd runs the session as `git`, but the entrypoint runs as root and creates `/run/secrets` with mode `700` and the key file with mode `600`.
- **The SSH session probably looks in a different place.** sshd does not pass the container environment to sessions. `Dispatcher__StorageSshPrivateKeyPath` therefore likely never reaches `SshSessionRunner`, which falls back to `/run/secrets/storage_ssh_key` from `appsettings.json`.
