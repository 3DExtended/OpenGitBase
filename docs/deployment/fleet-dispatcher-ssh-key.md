# Fleet dispatcher SSH key

The API holds one dispatcher→storage SSH key pair in the `FleetSecrets` table. The public key is stored in clear. The private key is AES-GCM encrypted with `EncryptSecret`, byte-for-byte, including the trailing newline that OpenSSH requires after the `END` marker.

## Consumers

| Consumer | What it does | When |
|----------|--------------|------|
| `POST /api/admin/fleet/dispatcher-ssh-keys/generate` (admin) | Replaces the key pair **and** the fleet bootstrap token. Returns the public key and the token; the private key stays in the API | On demand (`scripts/bootstrap-fleet.sh`) |
| Dispatcher [`entrypoint.sh`](../../applications/OpenGitBase.Dispatcher/entrypoint.sh) | Fetches the private key from `GET /api/v1/fleet/bootstrap/dispatcher-ssh-private-key` with `X-Fleet-Bootstrap-Token`, parses it with `jq`, checks it with `ssh-keygen -y`, and only then moves it to `/run/secrets/dispatcher_storage_ssh`, as `root:git` mode `640` in a `root:git` `750` directory. The SSH session runs as `git`, so it can read the key but not replace it. Next to the key it writes the SHA-256 of the token it used (`dispatcher_storage_ssh.token-sha256`). It fetches only when the key is missing or came from a different token, which happens after a rotation. On HTTP 401 it waits up to `DISPATCHER_KEY_PEER_WAIT_SECONDS` (default 30) for the other dispatcher to install the key fetched with the same token. On any failure it logs `entrypoint: WARNING: …`, keeps a key that is already there, and keeps Smart HTTP running. Ownership and mode are reapplied on every start. The token works **once** | Container start, after Smart HTTP is up |
| Dispatcher SSH session (`GitSessionProxyService`) | Uses the key to SSH into a storage node as `git` | SSH git only (`GIT_SSH_ENABLED=true`). Smart HTTP never reads it |
| Dispatcher `ssh-auth-hook.sh`, `dispatcher-wrapper.sh` | sshd starts both with a minimal environment, so neither sees the container's env vars. On every start, `entrypoint.sh` writes `DISPATCHER_API_URL` and every `Dispatcher__*` setting to `/etc/opengitbase/dispatcher-session.env`, and both scripts source that file. It also sets `Dispatcher__StorageSshPrivateKeyPath` to the path where the entrypoint installed the key. The wrapper execs the .NET session with these settings exported. The file holds no secrets; `FLEET_BOOTSTRAP_TOKEN` is not copied | SSH login and every SSH git session |
| Storage [`entrypoint.sh`](../../applications/repo-storage-layer/entrypoint.sh) | Writes `DISPATCHER_SSH_PUBLIC_KEY` (or the public key fetched from the API when that is empty) to `/home/git/.ssh/authorized_keys` | Every container start |

`/run/secrets` is the named volume `opengitbase_dispatcher_ssh` (Compose prefixes the project name), which both dispatchers mount. Because the token works once, only one fetch happens: whichever dispatcher starts first fetches, and the other one and every later recreate (`scripts/remote-rolling-update.sh --full` included) find the key on the volume. A new `FLEET_BOOTSTRAP_TOKEN` (after a regenerate) makes the next dispatcher to start fetch once more.

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
   - `FLEET_BOOTSTRAP_TOKEN` on `dispatcher-1` **and** `dispatcher-2` to the new token

3. Recreate the storage nodes **one at a time** so each rewrites `authorized_keys`. Wait for `(healthy)` before the next:

   ```bash
   docker compose up -d --no-deps --force-recreate storage-1
   ```

4. Recreate the dispatchers one at a time. `dispatcher-1` sees the new token and fetches the new key into the shared volume. `dispatcher-2` finds that key with the matching token hash and does not fetch:

   ```bash
   docker compose up -d --no-deps --force-recreate dispatcher-1
   docker compose up -d --no-deps --force-recreate dispatcher-2
   ```

   Recreate, not `docker restart`: a restart keeps the old environment, so it never sees the new token.

   Then run the dispatcher check above on both. Expect `valid`. `docker exec opengitbase_dispatcher_1 ssh-keygen -y -f /run/secrets/dispatcher_storage_ssh` must print the new public key (without the comment).
   `entrypoint: WARNING: keeping the storage SSH key …` means a dispatcher still has the old token: fix the override and recreate it.

## Turning on SSH git

`GIT_SSH_ENABLED=true` (`docker-compose.ssh.yml`) needs a usable key on the shared volume. On prod that means running [Regenerating](#regenerating) first: the stored key predates the normalization fix, and its token was used up in June 2026.

[`ssh-git-session-test.sh`](../../applications/OpenGitBase.Dispatcher/scripts/ssh-git-session-test.sh) covers the SSH path end to end: sshd login, the session environment, key permissions and the proxy to storage. [`entrypoint-key-bootstrap-test.sh`](../../applications/OpenGitBase.Dispatcher/scripts/entrypoint-key-bootstrap-test.sh) covers the key bootstrap, including the shared volume, recreates and rotation.
