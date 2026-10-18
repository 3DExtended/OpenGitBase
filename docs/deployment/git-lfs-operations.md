# Git LFS operations

How Git LFS runs on the storage fleet, what it costs in disk, how to roll it out and back, and which limits along
the request path decide the largest object that works. Design: [prd/git-lfs.md](../prd/git-lfs.md) (forge #234).

## Data flow

```
git-lfs client
     │  POST /{owner}/{repo}.git/info/lfs/objects/batch      (PAT, Basic auth)
     ▼
Cloudflare (prod) ─► HAProxy ─► Dispatcher ─► API: authorize, quota, plan actions, mint action tokens
                                   │
                                   │  PUT/GET .../info/lfs/objects/{oid}   (action token)
                                   ├── upload ───► write primary   (plaintext, SHA-256 checked while streaming)
                                   └── download ◄─ read replica, fallback write primary
                                   │
                                   │  POST .../objects/{oid}/verify
                                   ▼
                     API: stream from primary ─► segmented AES-256-GCM ─► encrypted replica A ┐ quorum: primary
                                                                      └─► encrypted replica B ┘ + ≥1 encrypted
                     then async: second encrypted copy, plaintext copy on a separate read replica
```

- **Upload.** The dispatcher streams the body to the write primary without buffering. Its body limit for that request
  is exactly the object size the batch declared.
- **Verify.** The upload is acknowledged (`LfsObject` row, quota charged) only once the primary and at least one
  encrypted replica hold the object. Otherwise verify answers `503` with `Retry-After`, and the client retries.
- **Push gate.** A push whose new pointers name objects without an `LfsObject` row is rejected with the oid, the path,
  and `git lfs push --all origin <ref>`.
- **Download.** The dispatcher tries the read replica first, then the primary. An object that is recorded but on no
  node answers `503` with `Retry-After`, and the API queues a repair.
- **Encryption.** The API encrypts and decrypts with the Repository Key, which never reaches a storage node.
  Encrypted replicas hold only ciphertext.

## Four-copy layout and disk sizing

| Copy | Node | Path | Volume |
|------|------|------|--------|
| Plaintext | write primary | `/srv/git/.lfs/{repositoryId}/{oid[0:2]}/{oid[2:4]}/{oid}` | `storageN_repos` |
| Plaintext | read replica (only when it is another node) | same | `storageN_repos` |
| Encrypted | encrypted replica A and B | `/var/lib/opengitbase/lfs/{repositoryId}/…/{oid}.aead` | `storageN_data` |

- The plaintext store sits outside the bare repository directory, so cold recovery and rebalance (which re-create
  the repository from git) leave it alone.
- The encrypted store is separate from the artifact tree, so watermark scans and artifact pruning never touch it.

**Sizing.** For an object of S bytes the fleet stores:

- S on the primary;
- S on the read replica, when that is another node;
- S + 29 bytes of header + 16 bytes per 1 MiB segment on each encrypted replica (about 0.0015 % overhead).

That is 3 × S when primary and read replica share a node (the compose default, `storage-1`), and 4 × S otherwise.
The repository quota counts S once, as `lfsBytes` next to `gitBytes`.

Plan the `_repos` volume of the plaintext node(s) for the LFS total, and each encrypted node's `_data` volume for its
share of the encrypted copies.

## Configuration

API section `Lfs` (compose: `Lfs__Enabled` and so on). Only the API reads it; dispatchers and storage need no LFS
settings.

| Key | Default | Meaning |
|-----|---------|---------|
| `Lfs:Enabled` | `false` | Off: the batch, transfer and verify endpoints answer `501` "Git LFS is not enabled on this server." |
| `Lfs:MaxObjectBytes` | 2 GiB | Largest object a batch accepts; bigger ones get a per-object `422`. See [Limits](#limits-along-the-request-path). |
| `Lfs:MaxBatchObjects` | 100 | Objects per batch (git-lfs sends 100); more answer `413`. |
| `Lfs:ActionTokenLifetimeSeconds` | 3600 | An action token must start its transfer within this time; a running transfer is not cut off. |
| `Lfs:ReplicaUploadTimeoutSeconds` | 600 | Cap for one encrypted replica upload during verify. |
| `Lfs:SegmentBytes` | 1 MiB | AEAD segment size for new encrypted copies. |
| `Lfs:MaxRepairsPerRun` | 100 | Copies one reconciler run repairs per repository; the rest follow in a queued run. |
| `Lfs:PublicBaseUrl` | empty | Base for action hrefs. Empty: the URL the client used, from `X-Forwarded-Proto` / `X-Forwarded-Host`. |

## Limits along the request path

The largest object that works is the smallest of these:

| Hop | Limit | Effect when exceeded |
|-----|-------|----------------------|
| API | `Lfs:MaxObjectBytes` (2 GiB) | Per-object `422` in the batch, with a message git-lfs shows |
| Repository quota | remaining bytes | `507` for the whole batch |
| HAProxy | `timeout client/server 5m`: inactivity, not total time | A verify or transfer silent for 5 minutes is cut off |
| Cloudflare (prod, proxied hostnames) | request body 100 MB (Free, Pro), 200 MB (Business), up to 5 GB (Enterprise) | Cloudflare answers `413` with an HTML page, before the dispatcher sees the request |
| Cloudflare | 125 s proxy read timeout (Enterprise can raise it) | `524` when the origin takes longer to answer, e.g. a verify that encrypts and uploads a big object |

**Production goes through the Cloudflare tunnel.** `opengitbase.com`, `www` and `api.opengitbase.com` are proxied
hostnames ([docker/cloudflare/config.yml](../../docker/cloudflare/config.yml)). With the 2 GiB default, objects
between the plan's body limit and 2 GiB pass the batch and then fail at Cloudflare with an HTML `413`. Before
enabling LFS in production, do one of the following:

- set `Lfs__MaxObjectBytes` to the plan's limit minus headroom, for example `104857600` (100 MiB) on Free or Pro, so
  users get the `422` message instead;
- or serve git and LFS on a DNS-only (not proxied) hostname and set `Git:PublicBaseUrl` (and `Lfs:PublicBaseUrl` if
  needed) to it.

**Production setting.** Since 2026-10-18 production runs with `Lfs__Enabled: "true"` and
`Lfs__MaxObjectBytes: "104857600"` (100 MiB) in the `x-api-env` block of the Tower override.

A verify of an object near the limit must also finish within Cloudflare's 125 s. At 100 MiB this is well inside the
limit on the internal network.

## Rollout

The order keeps mixed versions from accepting an upload they cannot replicate.

1. **API** with `Lfs:Enabled=false`, one instance at a time. It understands the pointer data the new pre-receive hook
   sends, and old hooks keep working.
2. **Storage nodes**, one at a time, the primary last. On Tower, `storage-1` is the primary for most repositories;
   `rolling-update.sh --full` rolls 2, 3, 4, then 1. The new endpoints stay unused until the API calls them.
3. **Dispatchers**, one at a time.
4. **Enable.** Set `Lfs__Enabled: "true"` (and the chosen `Lfs__MaxObjectBytes`) in the API environment on Tower
   (`docker-compose.override.yml`). Then recreate `api-2` and `api-1` one at a time
   (`docker compose up -d --no-deps --force-recreate --wait api-N`).

`scripts/remote-rolling-update.sh` without `--full` rolls API and web, so it covers step 1. `--full` then rolls
storage, the API again (harmless) and the dispatchers, which covers steps 2 and 3.

**Smoke test** after step 4, with a real file:

```bash
git clone https://<host>/<owner>/<repo>.git && cd <repo>
git lfs install --local && git lfs track "*.bin"
head -c 5000000 /dev/urandom > smoke.bin && git add .gitattributes smoke.bin && git commit -m "lfs smoke"
git push origin HEAD
cd .. && git clone https://<host>/<owner>/<repo>.git smoke-clone && cmp <repo>/smoke.bin smoke-clone/smoke.bin
```

Then check the admin repository page: LFS objects 1, no missing copies once the async copies are done.

## Rollback

In reverse, disabling first:

1. Set `Lfs__Enabled: "false"` and recreate `api-2` and `api-1`. Clients get `501`; pushes without LFS are unaffected.
2. Dispatchers, then storage nodes, then the API back to the previous image, if needed.

Recorded objects stay on disk and in the database, and are served again once LFS is re-enabled. Because of the push
gate, a ref never points at an object the server does not hold.

## SSH remotes

LFS over SSH (`git-lfs-authenticate`, `git-lfs-transfer`) is not supported. Users with an SSH remote point LFS at the
HTTPS endpoint and authenticate with a personal access token:

```bash
git config lfs.url https://<host>/<owner>/<repo>.git/info/lfs
```

## Health and repair

- **Admin → Repositories.** The list shows fleet LFS totals and missing copies. A repository page shows objects,
  bytes, the copies each node holds or misses, and "Restoring LFS objects to {node}" during cold recovery.
- **Reconciler** (each API at start-up, then every `HaStorageBackground:ReconcilerIntervalSeconds`, default 900 s):
  - **"LFS copies"** compares every node's on-disk LFS inventory with the database. It re-encrypts missing encrypted
    copies and copies or decrypts missing plaintext copies. Decisions come from disk, not from `LfsObjectCopy`.
  - **"LFS orphans"** deletes LFS data of repositories that no longer exist, including on nodes that were down
    during the deletion. A database without any repository deletes nothing.
- **Hot promotion** queues an LFS repair at once.
- **Cold recovery** imports git onto a healthy node that holds no encrypted copy of the repository, when there is
  one, so both encrypted copies stay. The node becomes the repository's plaintext primary. The repository is
  writable once git is restored, and the LFS objects are decrypted back from an encrypted replica in the background.
- **Fleet drift** (`GET /admin/fleet/drift`) reports `OrphanLfs` for LFS directories of deleted repositories.
- **Repository deletion** removes the LFS rows and calls the per-repository LFS delete on every node that held data.

## Troubleshooting

| Symptom | Cause |
|---------|-------|
| `501` "Git LFS is not enabled on this server." | `Lfs:Enabled` is false on the API that answered |
| `503` "LFS requires encrypted replication; this repository is still migrating." | The repository has no healthy encrypted replica yet (RF3 or migrating) |
| `503` + `Retry-After` on verify | Quorum not reached; the client retries, and a repeated upload is accepted unchanged |
| `503` + `Retry-After` on download | Recorded, but on no node right now; a repair is queued |
| Per-object `422` "too large" | Above `Lfs:MaxObjectBytes` |
| `507` | Repository quota exceeded |
| `413` with an HTML body | Cloudflare's body limit; see [Limits](#limits-along-the-request-path) |
| `524` | Cloudflare's 125 s read timeout, typically a verify of a large object |
| Push rejected: "LFS object … is missing on the server" | Objects not uploaded; run `git lfs push --all origin <ref>` |
