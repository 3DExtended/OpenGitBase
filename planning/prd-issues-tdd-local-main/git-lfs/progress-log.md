# Progress Log — Git LFS

Sequential run log for `/prd-issues-tdd-local-main` over PRD #234 (slices #235–#243).

| When | Item | Action | Result |
|------|------|--------|--------|
| 2026-10-15 | lfs-01 | Tracer bullet: batch API, action tokens, streaming transfer, verify, LfsObject table | 927eaf1 |
| 2026-10-15 | lfs-02 | Permissions (403 vs 404), object/batch caps, quota 507, usage accounting, gitBytes/lfsBytes | 7305f3e |
| 2026-10-15 | lfs-03 | Segmented AEAD in the API, encrypted store + role enforcement, verify = LFS Quorum, async copies, LfsObjectCopy, Retry-After, read routing | f4d1e44 |
| 2026-10-15 | lfs-04 | Pre-receive pointer gate: new pointers detected on storage, missing LfsObject rejects with oid, path and `git lfs push --all` fix; E2E harness drains stdout/stderr concurrently | ea8e6da |
| 2026-10-15 | lfs-05 | On-disk LFS inventory, LfsCopyReconciler (compare, LastVerifiedAt, repair plaintext/encrypted/decrypt), LfsRepairQueue worker, reconciler step "LFS copies", dispatcher 503 + repair request | baa1dfd |
| 2026-10-15 | lfs-06 | Cold recovery queues the LFS restore (Primary row for a new target, lost plaintext rows forgotten), hot promotion queues a repair, corrupt encrypted copy detected and re-encrypted, GetLfsCopyStatusQuery | 53e640e |
| 2026-10-15 | lfs-07 | One pointer parser on storage; blob endpoint describes the LFS object (+ `available`); raw streams the object via read replica first (allowlisted image types, else octet-stream, `nosniff`; 404 never uploaded, 503 + Retry-After + repair when no node holds it); blob viewer badge, image preview ≤ 20 MiB, download-only, "not available"; Git/LFS split in the usage meter | b6f4f5b |
| 2026-10-15 | lfs-08 | Per-repository LFS DELETE (idempotent, both roots) on every node that held data after the repository row goes; reconciler step "LFS orphans" cleans nodes that were down (empty-DB guard); `OrphanLfs` fleet drift; admin LFS figures per repository (copies missing per node, restore progress) and fleet totals in API and web | 70aacd3 |
| 2026-10-15 | lfs-09 | Cold recovery prefers a node without an encrypted copy; bundle import records the node as primary; chaos E2E (cold recovery + fallback) and 120 MiB E2E written; operations docs (sizing, limits incl. Cloudflare 100 MB / 125 s, rollout/rollback, SSH); compose run blocked by Docker Desktop; production HITL open | fae6159 |
| 2026-10-15 | lfs-09 | Compose E2E green on hot-patched containers (Docker builds still hang): `GitHttps.Lfs*` 9/9 incl. lfs-08 deletion and 120 MiB, `LfsColdRecoveryChaosTests` (FullHa) 2 m 10 s; production HITL open | fae6159 (code under test) |
| 2026-10-18 | lfs-09 | Merged with main (push-quota fix carried into `RepositoryAccessCheckEvaluator`); production deploy in the documented order; LFS enabled with 100 MiB per object; smoke test (5 MB object, three copies, byte-identical clone) green; locks/verify hint open | e372f84 |
| 2026-10-18 | lfs-09 | `locks/verify` answers `501` instead of `200` "no locks", so git-lfs turns lock verification off once and pushes without the "Locking support detected" hint (probed with git-lfs 3.7.1) | 2fa073e |
