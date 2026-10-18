# lfs-09 — LFS chaos E2E and production rollout

## Summary

- **Cold recovery fixes.** Writing the chaos scenario showed two gaps that kept the LFS restore after cold recovery
  (lfs-06) from working in a common layout.
  - **Target choice.** `ColdRecoveryService` picked the first healthy node not named storage-2 or storage-3.
    Encrypted placement follows free space, so a repository's encrypted copies can sit on storage-4. The recovery
    then imported git onto its own encrypted replica, and one encrypted copy was left.
    - It now prefers a healthy node that holds no encrypted copy of the repository, and among those, nodes outside
      the platform's encrypted layout.
    - Only when every healthy node holds an encrypted copy does one of them become the primary.
  - **Node role.** `POST /internal/repos/import-bundle` did not touch the node's role file. A node that had been the
    repository's encrypted replica kept refusing plaintext LFS (403) and git content after it became the primary.
    The import now records the node as the repository's `Primary`, since cold recovery is its only caller.
- **Chaos E2E** (`tests/OpenGitBase.E2E.Tests`):
  - `HaChaos/LfsColdRecoveryChaosTests` (new, FullHa):
    - storage-1 takes no new repositories while the test repository is created (admin capacity cap, restored at
      once), so stopping its primary does not cold-recover every repository on the stack;
    - the primary loses its LFS data and is stopped; the test marks stale nodes via the fleet drift report;
    - cold recovery must move the primary to the node without a copy and keep both encrypted replicas;
    - a clone must return the object byte-identical, and it must be decrypted onto the new primary;
    - the old primary returns without the object, and a second clone must fall back to the new primary.
  - `GitHttps/LfsLargeObjectTests` (new): a 120 MiB object through the dispatcher upload, verify with encryption, the
    copies (each encrypted copy checked for its exact AEAD size), a clone, and the web raw download through the API.
  - Scenarios covered by earlier slices: the encrypted LFS directory deleted while the database says healthy
    (`LfsReconcilerRepairTests`, lfs-05), and repository deletion (`LfsRepositoryDeletionTests`, lfs-08).
  - **Hot promotion is not reachable in compose.** The planner always places the read replica on the primary's node
    (`ReplicaSetPlanner`), so no repository has a separate `ReadReplica` row, and failover can only cold-recover.
    The promotion path is covered by `PromotePrimaryReplicaQueryHandlerTests` (lfs-06).
- **Operations docs.**
  - `docs/deployment/git-lfs-operations.md` covers:
    - data flow and the four-copy layout with paths and volumes;
    - sizing (3 × S with primary and read replica on one node, 4 × S otherwise);
    - configuration, and the limits along the request path;
    - rollout and rollback order, the smoke test, SSH `lfs.url`, health and repair, and troubleshooting.
  - `docs/PROJECT-STATE.md` lists LFS in the storage table, the encryption posture and the doc index.
  - **Finding for the rollout.** Production goes through the Cloudflare tunnel. Proxied hostnames accept request
    bodies up to 100 MB on Free/Pro (200 MB Business) and wait at most 125 s for the origin.
    - With the 2 GiB `Lfs:MaxObjectBytes` default, a larger upload passes the batch and then fails at Cloudflare
      with an HTML 413.
    - Before enabling, either set `Lfs__MaxObjectBytes` (for example 100 MiB) or serve git on a DNS-only hostname.

## Linked Context

- PRD: `docs/prd/git-lfs.md` (forge #234)
- Work item: `lfs-09` (forge #243)

## Dependency Graph

### Direct dependencies (blocked by)

- lfs-03, lfs-04, lfs-05, lfs-06, lfs-07, lfs-08

### Full chain

`lfs-01 -> lfs-02 -> … -> lfs-08 -> lfs-09`

## Status

- Branch: `claude/lfs-implementation-difficulty-1a3fd2`
- Tests:
  - Storage, `python3 -m unittest discover`: 168 ran. The one error is the existing `test_storage_artifact_crypto`
    import. New: `test_importing_a_bundle_makes_a_former_encrypted_replica_the_plaintext_primary`.
  - `dotnet test tests/OpenGitBase.Api.Tests`: 888 passed. New: `ColdRecoveryServiceTests`
    `TryRecoverAsync_PrefersANodeWithoutACopyOverOneOfTheEncryptedReplicas`. A mutation check (the old target choice)
    fails it.
  - E2E meta tests (`RequiresCompose!=true`): 29 passed.
- **Compose E2E: passed.**
  - **Docker trouble during the slice.** Docker Desktop on this host hung for a while:
    - image builds and `docker pull` stopped at registry metadata, for other projects' builds too;
    - containers were SIGKILLed in a loop, and api-1 hung after a start-up migration failure while Postgres
      recovered.

    After recreating `ssh-lb` and restarting api-1, the stack was stable. Builds still hung, so the new code went
    into the running containers directly:
    - storage: `storage-http-server.py` and `storage_lfs.py` via `docker cp`, restarting the nodes one at a time;
    - API: the 30 `OpenGitBase.*.dll` from a local `dotnet publish -c Release`. All third-party assemblies were
      byte-identical to the image's.

    Recreating those containers brings back the lfs-07 image until the images are rebuilt.
  - `GitHttps.Lfs*`: 9/9 passed. That covers lfs-01..08, plus the new `LfsRepositoryDeletionTests` and
    `LfsLargeObjectTests`, the 120 MiB object at 22 s.
  - `HaChaos.LfsColdRecoveryChaosTests` (`OPENGITBASE_E2E_COMPOSE_PROFILE=FullHa`) passed in 2 m 10 s. Afterwards
    all four nodes were healthy and storage-1's capacity was back to unlimited.
- **Production (HITL): not started.** The deploy order and the `Lfs:Enabled` switch need a human decision. The
  Cloudflare body limit needs one too.
- Commit(s): see `progress-log.md`
