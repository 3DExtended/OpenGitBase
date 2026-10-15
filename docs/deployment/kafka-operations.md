# Kafka (KRaft) operations

OpenGitBase runs a **3-broker Apache Kafka** cluster in KRaft mode (compose services `kafka-1` / `kafka-2` / `kafka-3`, RF=3 topics). On a single host this is not multi-machine HA, but durable data dirs keep voter metadata across container restarts so the quorum does not wedge.

## Data placement

Each broker keeps its KRaft log at `/var/lib/kafka/data` (`KAFKA_LOG_DIRS`). A one-shot `kafka-vol-init` service mounts the same three dirs at `/kafka1`..`/kafka3` and `chown`s them to uid/gid `1000` (apache/kafka `appuser`), so root-owned volumes or bind dirs do not crash-loop brokers.

| Stack | Compose files | Kafka data |
|-------|---------------|------------|
| Dev, CI, E2E | `docker-compose.yml` (+ `docker-compose.e2e.yml`, `docker-compose.e2e-full-ha.yml`) | Named volumes `kafka{N}_data` |
| Production overlay | `docker-compose.yml` + `docker-compose.prod.yml` | Bind mounts `${OGB_KAFKA_DATA_ROOT:-/mnt/cache/appdata/opengitbase}/kafka{N}` |
| Tower (Unraid) | `docker-compose.yml` + `docker-compose.override.yml` | The same bind mounts, copied into the untracked override from the commented block in `docker-compose.override.example.yml` |

**Why the SSD cache pool (#232):** the KRaft `__cluster_metadata` log fsyncs on every append. In named volumes on Tower's array-backed `docker.img`, fsync took ~273 ms (vs ~3 ms on the cache pool) and routinely exceeded the 2 s `controller.quorum.request.timeout.ms`, so the controller lost leadership and re-elected forever. Before blaming Kafka for election churn, probe the data dir: `dd if=/dev/zero of=<dir>/.t bs=4k count=100 oflag=dsync`.

The base file cannot carry the bind mounts: Docker Desktop refuses paths it does not share (`mounts denied: The path /mnt/cache/... is not shared from the host`). Overlays replace the named volumes because Compose merges service `volumes` by mount target.

**Guard:** `scripts/kafka-storage.sh` makes `rolling-update.sh`, `kafka-quorum-reset.sh` and `kafka-quorum-smoke.sh` refuse to run on a host that has `/mnt/cache/appdata/opengitbase/kafka1` unless the compose files in use bind-mount all three dirs for the brokers **and** `kafka-vol-init`. A plain `docker compose up` (e.g. `up -d api-1` without `--no-deps`, which reaches `kafka-init` and the brokers) is not guarded, so keep the override block in place on Tower. `scripts/test-kafka-data-placement.sh` covers the compose files, the guard and the wipe.

Do **not** recreate a single broker alone after a failure — treat the quorum as one unit.

## Lifecycle script

```bash
# Normal restart (keeps volumes; safe after docker/daemon restarts)
./scripts/kafka-quorum-reset.sh --restart

# Clean slate (empties all three Kafka data dirs, re-bootstraps topics)
./scripts/kafka-quorum-reset.sh --wipe
```

Both modes:

1. Stop/remove `kafka-1` `kafka-2` `kafka-3` `kafka-init` together
2. Optionally wipe (`--wipe`): remove the `kafka{N}_data` named volumes (on Tower only stale pre-#232 copies) and empty whatever `kafka-vol-init` mounts — the named volumes, or the SSD bind dirs on Tower. Git repos, Postgres and MinIO data are never touched.
3. Start all three brokers together and wait until healthy
4. Run `kafka-init` (creates `git.push.received`, `ci.job.available`, `ci.job.cancelled`)
5. POST `/api/v1/internal/pipelines/kafka-wake-republish` from inside the compose network (queued/cancelled job wakes). Host curls to `:8089` are skipped when they would fail InternalNetwork checks.

Pass `--no-republish` to skip step 5.

## Rolling updates

`./scripts/rolling-update.sh` does **not** touch Kafka by default. Pass `--kafka` to atomically restart the quorum (keep volumes) after the app roll:

```bash
./scripts/rolling-update.sh --kafka
```

Never use a plain `docker compose up` that reconciles Kafka mid-deploy without `--no-deps` on unrelated services.

## Push durability (outbox)

Ingest writes a Postgres **`GitPushOutbox`** row first. A background worker schedules pipeline runs from that table and best-effort publishes to Kafka. A Kafka wipe or outage does **not** drop ingested pushes. Job claim/cancel remain sourced from Postgres; wake republish restores low-latency platform-agent signals.

## Tower cutover (first durable deploy)

If brokers are already wedged (endless Raft elections / `Connection refused`):

```bash
cd /mnt/user/projects/openGitBase
git pull
./scripts/kafka-quorum-reset.sh --wipe
```

After that, prefer `--restart` (or rolling-update without Kafka) — volumes keep the quorum alive across restarts.

## Verification

```bash
docker ps --filter name=kafka --format 'table {{.Names}}\t{{.Status}}'
curl -sS https://api.opengitbase.com/api/public/status   # Message bus group = Healthy
./scripts/kafka-quorum-smoke.sh                          # local restart-survives check
```
