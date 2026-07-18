# Kafka (KRaft) operations

OpenGitBase runs a **3-broker Apache Kafka** cluster in KRaft mode (compose services `kafka-1` / `kafka-2` / `kafka-3`, RF=3 topics). On a single host this is not multi-machine HA, but durable named volumes keep voter metadata across container restarts so the quorum does not wedge.

## Durable volumes

Each broker mounts `kafka{N}_data` at `/var/lib/kafka/data` (`KAFKA_LOG_DIRS`). A one-shot `kafka-vol-init` service `chown`s those volumes to uid/gid `1000` (apache/kafka `appuser`) so Unraid/root-owned named volumes do not crash-loop brokers.

Do **not** recreate a single broker alone after a failure — treat the quorum as one unit.

## Lifecycle script

```bash
# Normal restart (keeps volumes; safe after docker/daemon restarts)
./scripts/kafka-quorum-reset.sh --restart

# Clean slate (removes all three Kafka volumes, re-bootstraps topics)
./scripts/kafka-quorum-reset.sh --wipe
```

Both modes:

1. Stop/remove `kafka-1` `kafka-2` `kafka-3` `kafka-init` together
2. Optionally wipe volumes (`--wipe`)
3. Start all three brokers together and wait until healthy
4. Run `kafka-init` (creates `git.push.received`, `ci.job.available`, `ci.job.cancelled`)
5. POST `/api/v1/internal/pipelines/kafka-wake-republish` (queued/cancelled job wakes)

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
