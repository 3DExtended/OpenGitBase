# Slice triage review todo

Post-audit HITL checklist for **open** forge `[slice]` discussions on `opengitbase/open-git-base`.

## Purpose

An agent audit of slices #29–#212 left many discussions open (partial / blocked / complete-but-not-closed).
This list is for **human-in-the-loop review**: decide close, keep open, descope, or block — one slice at a time.

**How to use:** open a new agent chat and paste `planning/slice-triage-hitl-review-prompt.md`.
Point the agent at this file. Work top-to-bottom (or jump to a section). Check boxes as you finish each item.

**Out of scope:** closed/resolved slices (not listed). Do not close PRDs/ADRs in this pass.

## Summary

- **Open slices in this list:** 164 (live `ogb issue list --status open`, `[slice]` only)
- **Audited slices with local comments:** 184 under `/tmp/ogb-slice-*-comment.md`
- **Audited but no longer open:** 20 (likely closed as complete during audit; numbers: #29, #35, #39, #66, #68, #69, #70, #71, #83, #119, #120, #137, #140, #165, #166, #169, #177, #182, #183, #187)
- **Verdict mix (open):** partial=136, complete=27, blocked=1
- **Blocked:** #135 (ers-17)
- **Close candidates** (audit verdict `complete`, still open): 27

### Top themes by open count

- **Admin replication UI** (`admin-repl`): 4
- **CI/CD pipelines & runtime** (`ci`): 18
- **Security / code-review remediation** (`sec`): 6
- **Commit change view & fixes** (`cv`): 7
- **Unified E2E framework** (`e2e`): 16
- **E2E test population** (`pop`): 30
- **Encrypted replica storage** (`ers`): 15
- **Git HTTPS / PAT** (`git-https`): 6
- **Git storage proxy & multi-node** (`storage-proxy`): 5
- **HA storage replication (RF=3)** (`ha-storage`): 12
- **Merge requests** (`mr`): 13
- **Public status dashboard** (`status`): 6
- **Discussions & sub-threads** (`disc`): 15
- **Repository web browsing** (`repo-browse`): 11

### Markers

- **BLOCKED** — cannot proceed without design/product answer
- **HUMAN-DECISION** — audit calls out an explicit product/design choice
- **CLOSE-CANDIDATE** — audit verdict `complete`; confirm whether to close

Local audit sources per slice: `/tmp/ogb-slice-{N}-comment.md` (and `/tmp/ogb-slice-{N}.md`).

## Admin replication UI

_4 open_

- [ ] #30 — admin-repl-02 — Storage page fleet replication card
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/30
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Teaser attention filter should rely on server-side list API semantics (e.g. dedicated needs-attention query or documented severity-only fetch) instead of duplicating ReplicationAttention.NeedsAtten…
    - Rollup counts only the first 100 repositories (pageSize: 100 L75); large fleets will under-report state totals until pagination or a rollup endpoint is used
    - “View all →” could pass ?sort=severity explicitly so navigation intent is preserved in the URL

- [ ] #31 — admin-repl-03 — Admin navigation and repository index
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/31
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Table columns for oldest last synced (oldestLastSyncedAt) and epoch (replicationEpoch) — API DTO and i18n keys exist (api.ts, en.json) but index table does not render them
    - Pager should always show total count, not only when totalCount > pageSize
    - Replication tile should be the second tile on admin home (after Storage); currently 4th after Compute and Status

- [ ] #32 — admin-repl-04 — Repository replication detail page
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/32
  - Verdict: `partial` · confidence: `medium`
  - Why open / gaps:
    - Primary replica card should display lastSyncedAt to satisfy “each replica” row spec (user story 22).
    - API test should assert resolved NodeId (e.g. "storage-1") on replica DTO now that the field exists server-side.
    - Each replica shows node id, watermarks, in-sync, last synced, lag delta

- [ ] #33 — admin-repl-05 — Cross-surface polish and regression smoke
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/33
  - Verdict: `partial` · confidence: `medium`
  - Why open / gaps:
    - Fix client/server attention drift for Rf4Healthy (and verify Rf4Migrating / Recovering parity) so storage teaser severity matches list API semantics
    - Complete manual smoke checklist in planning/prd-issues-tdd-local/admin-replication-ui/progress-log.md or add Playwright route smoke for /admin/storage, /admin/repositories, /admin/repositories/[id]
    - Update docs/issues/admin-replication-ui/README.md slice statuses (still all ready) and note ha-storage-11 supersession in planning docs

## CI/CD pipelines & runtime

_18 open_

- [ ] #34 — ci-01 — Compose foundation: Kafka + MinIO
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/34
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Operator documentation listing Kafka and Layer Store connection variables (Kafka__BootstrapServers, LayerStore__Endpoint, etc.) and their compose defaults
    - README or operator guide section describing Kafka (3-broker KRaft, host port 9092) and MinIO (API 9000, console 9001) services
    - API (or a shared env contract) documents KAFKA_* and LAYER_STORE_* connection variables

- [ ] #36 — ci-03 — Push trigger → Pipeline Run (no execution)
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/36
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Automated integration test covering both paths: push with .opengitbase-ci.yml → PipelineRun row exists; push without CI file → no row
    - Unit tests for IngestGitPushQueryHandler and SchedulePipelineRunFromPushQueryHandler (currently empty stub classes)
    - Integration test: push with CI file → run row exists; push without → no row

- [ ] #37 — ci-04 — Pipeline run read API + empty state
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/37
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - API pagination (page/cursor params and response envelope) for pipeline run listing
    - CI configuration detection (no runs and no .opengitbase-ci.yml at default branch) before showing the CI Not Configured empty state
    - Automated test asserting both empty-state and populated-list behavior (API or UI)

- [ ] #38 — ci-05 — Compute node registry + platform enrollment
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/38
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - End-to-end integration test chaining platform enrollment → agent registration → heartbeat → ogb-hosted job claim eligibility
    - Handler tests for capacity rejection, missing-capacity rejection, heartbeat, register, and list-nodes (several test classes are empty coverage stubs in ComputeNodeQueryHandlerTests.cs)
    - Capacity update guard for CPU/memory “equivalent resource breach” beyond concurrent-job count (if required by story 40)

- [ ] #40 — ci-07 — Job queue, claim API, Job Identity
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/40
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Single integration test asserting JobIdentityEntity.RevokedAt is set after job passes/fails/cancels (handler implements revocation; test gap only)
    - Optional hardening: no automated test verifies IJobAvailableEventPublisher.PublishAsync is invoked on scheduler enqueue (mocked in test scopes)
    - Integration test: enqueue → claim → status update → identity revoked on completion stub

- [ ] #41 — ci-08 — Compute agent runtime
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/41
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Integration test exercising the compute agent service loop end-to-end (register/heartbeat/claim/execute/report), not just isolated API handlers with manual status updates
    - Integration test: enrolled agent claims and completes a stub job end-to-end

- [ ] #42 — ci-09 — Base Image Catalog + Layer Store seed
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/42
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Admin API CRUD (slug, version label, artifact URI, OCI provenance)
    - Parser/catalog validation rejects unknown image: slugs at schedule or claim time
    - Agent fetches base layer from Layer Store using Job Identity

- [ ] #43 — ci-10 — Tracer: first `ogb-hosted` job end-to-end
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/43
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Firecracker MicroVM + OverlayFS from catalog base image
    - MicroVM destroyed + Job Identity revoked
    - Demo: push .opengitbase-ci.yml with one ogb-hosted job → run passes

- [ ] #44 — ci-11 — Staged pipelines + `only` globs
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/44
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - keep open

- [ ] #45 — ci-12 — CI variables + `GIT_DEPTH` materialization
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/45
  - Verdict: `partial` · confidence: `medium`
  - Why open / gaps:
    - Reserved CI_* override rejected
    - GIT_DEPTH: 0 full worktree at $CI_PROJECT_DIR
    - Integration test: logs echo CI_COMMIT_SHA + custom var

- [ ] #46 — ci-13 — Dependency live install + telemetry
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/46
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - installscript runs as root inside guest
    - No promoted layer → live install + upper OverlayFS capture
    - Logs: per-dependency promoted layer vs live install

- [ ] #47 — ci-14 — Layer promotion admin + promoted mounts
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/47
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Promotion job on platform node + upload to Layer Store
    - Integration test: 5 installs → promote → 6th skips install

- [ ] #48 — ci-15 — Hybrid `runs-on` routing
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/48
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Single pipeline stage runs jobs on platform and org nodes in parallel
    - No eligible node → job stays queued with observable reason
    - Integration test: same-stage jobs with different runs-on land on correct node types

- [ ] #49 — ci-16 — Egress allowlists + domain requests
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/49
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - End-to-end integration test for egress deny/approve workflow (explicit AC #8).
    - Pipeline job failure UI to submit a Domain Allowance Request in-context (prefill domain from denial log).
    - API/controller tests for egress endpoints (OrganizationPipelineControllerTests is empty stub).

- [ ] #50 — ci-17 — Platform agent Kafka job wake
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/50
  - Verdict: `partial` · confidence: `medium`
  - Why open / gaps:
    - Platform agents subscribe to ci.job.available on enrollment

- [ ] #51 — ci-18 — Job timeout, cancel, resource limits
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/51
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Runtime CPU/memory policy enforcement with logged termination reason (watchdog or host monitor).
    - Integration test proving cancel mid-run stops execution and timeout fires on a sleep job.
    - v1 policy table for timeout overrides (optional if hardcoded defaults are acceptable for v1).

- [ ] #52 — ci-19 — Pipeline UI: detail, logs, cancel, commit badge
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/52
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Run detail shows stages, jobs, statuses, durations
    - Cancel button visible only to write-access users

- [ ] #53 — ci-20 — Compose E2E: push → green pipeline
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/53
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Failure output identifies which stage/job broke

## Security / code-review remediation

_6 open_

- [ ] #54 — sec-01 — Production MSW and test artifact lockdown — **CLOSE-CANDIDATE**
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/54
  - Verdict: `complete` · confidence: `high`
  - Why open / gaps:
    - Audit marked complete but discussion still open — confirm close vs residual follow-ups

- [ ] #55 — sec-02 — Internal network trust behind reverse proxy — **CLOSE-CANDIDATE**
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/55
  - Verdict: `complete` · confidence: `high`
  - Why open / gaps:
    - Audit marked complete but discussion still open — confirm close vs residual follow-ups

- [ ] #56 — sec-03 — Repository access checks and DTO field redaction
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/56
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Access-check success omits topology OR dispatcher-only
    - Tests cover anonymous/outsider/member/owner matrix

- [ ] #57 — sec-04 — Storage destructive ops and push enforcement hardening
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/57
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Bind :8082 to localhost, require mTLS, or enforce proxy-only access so arbitrary containers on the Docker network cannot anonymously push/fetch; document the expected network layout
    - HTTP integration test proving DELETE /internal/repos succeeds for a valid per-repo path after provisioning
    - DELETE with valid per-repo path still works in integration test

- [ ] #58 — sec-05 — Production secrets and compose profile separation — **CLOSE-CANDIDATE**
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/58
  - Verdict: `complete` · confidence: `high`
  - Why open / gaps:
    - Audit marked complete but discussion still open — confirm close vs residual follow-ups

- [ ] #59 — sec-06 — Web auth redirect and site gate policy — **CLOSE-CANDIDATE**
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/59
  - Verdict: `complete` · confidence: `high`
  - Why open / gaps:
    - Audit marked complete but discussion still open — confirm close vs residual follow-ups

## Commit change view & fixes

_7 open_

- [ ] #60 — fix-01 — Commit page navigation and error parity
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/60
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Cross-repo navigation with same SHA
    - Stale-response guard on rapid param changes
    - Empty root file list empty state

- [ ] #61 — fix-02 — MR page error handling and review thread correctness — **CLOSE-CANDIDATE**
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/61
  - Verdict: `complete` · confidence: `high`
  - Why open / gaps:
    - Audit marked complete but discussion still open — confirm close vs residual follow-ups

- [ ] #62 — fix-03 — Commit change view test coverage gaps
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/62
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Compose E2E or documented smoke beyond API-only

- [ ] #63 — cv-01 — Storage commit read helpers — **CLOSE-CANDIDATE**
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/63
  - Verdict: `complete` · confidence: `high`
  - Why open / gaps:
    - Audit marked complete but discussion still open — confirm close vs residual follow-ups

- [ ] #64 — cv-02 — Commit read API
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/64
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Ambiguous prefix → 404
    - Integration tests: linear diff, root, prefix, ambiguous, auth
    - OpenAPI / generated client updated

- [ ] #65 — cv-03 — Shared unified diff viewer
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/65
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Playwright visual regression test that navigates to MR Changes tab and captures a snapshot (with review threads visible), per acceptance criterion 5
    - Playwright visual regression: MR Changes tab snapshot matches pre-refactor baseline

- [ ] #67 — cv-05 — RepoCommitLink and MR Commits tab
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/67
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Dedicated Playwright snapshot of MR Commits tab showing clickable commit cards (hover/focus styling).
    - Compose-stack E2E/UI path: multi-commit seed → open MR → Commits tab → click commit → assert message/diff → back link to MR detail.
    - Optional hardening (tracked separately in fix-03): repoCommitPath / RepoCommitLink unit tests.

## Unified E2E framework

_16 open_

- [ ] #72 — e2e-03 — Baseline manager + update-baselines
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/72
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Dedicated unit tests for BaselineManager diff engine using fixture golden directories (compare/update/missing-baseline paths)
    - Optional polish: missing-baseline failure message in E2eTestBase.AssertBaselinesAsync does not yet mention --update-baselines (slice “What to build” #5)
    - Unit tests for normalizer and diff engine with fixture golden directories

- [ ] #73 — e2e-04 — Static HTML report + browser open flags
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/73
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Failed run produces browsable index.html with transcript and diff section
    - latest report path updated each run
    - Report generator covered by unit tests

- [ ] #74 — e2e-05 — Tier orchestrator + skip recording — **CLOSE-CANDIDATE**
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/74
  - Verdict: `complete` · confidence: `medium`
  - Why open / gaps:
    - No dedicated unit tests for TierOrchestrator (BuildSkipSummaries, BuildDotnetTestFilter) or runner fail-fast/skip integration
    - Slice demo scenario (Tier 0 pass → Tier 2 partial fail → UI skipped) not captured as an automated test

- [ ] #75 — e2e-06 — Test isolation + baseline normalization
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/75
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - DB reset runs between tests; no cross-test pollution in feature tier demo
    - Normalizer unit tests cover token replacement rules
    - Database reset between tests

- [ ] #76 — e2e-07 — Capturing SendGrid sender + E2E mail API — **CLOSE-CANDIDATE**
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/76
  - Verdict: `complete` · confidence: `high`
  - Why open / gaps:
    - Audit marked complete but discussion still open — confirm close vs residual follow-ups

- [ ] #77 — e2e-08 — Identity seed tier + auth journey test — **CLOSE-CANDIDATE**
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/77
  - Verdict: `complete` · confidence: `high`
  - Why open / gaps:
    - Audit marked complete but discussion still open — confirm close vs residual follow-ups

- [ ] #78 — e2e-09 — Git facade + HTTPS PAT scenario — **CLOSE-CANDIDATE**
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/78
  - Verdict: `complete` · confidence: `high`
  - Why open / gaps:
    - Audit marked complete but discussion still open — confirm close vs residual follow-ups

- [ ] #79 — e2e-10 — Playwright invoker + UI tier + report embed — **CLOSE-CANDIDATE**
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/79
  - Verdict: `complete` · confidence: `high`
  - Why open / gaps:
    - Audit marked complete but discussion still open — confirm close vs residual follow-ups

- [ ] #80 — e2e-11 — URL discovery + Discovered skeleton generator
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/80
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Visited pages scanned for links not in coverage registry
    - New URL generates skeleton test under Discovered/
    - Generated test fails without committed baseline

- [ ] #81 — e2e-12 — Curated security auth matrix
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/81
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Table-driven matrix on one protected resource across anonymous, outsider, reader, writer, and admin/owner (slice spec; RepositoryMemberAuthMatrixTests covers members but is a separate population sl…
    - Curated adversarial cases: missing auth header, swapped/wrong resource id, invalid JSON body with expected 401/403/404 (not 500)
    - E2eApiClient extension to send raw malformed requests without throwing before capture (FuzzRunner uses raw HttpClient/StringContent in PlaywrightAndDiscovery.cs, not the shared API client + transcr…

- [ ] #82 — e2e-13 — Optional fuzz tier
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/82
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Dedicated fuzz results section in HTML report (clearly marked non-baseline area per slice spec)
    - Two additional mutation scenarios (strip auth, swap GUID, truncate body, garbage types — slice requires ≥3)
    - Test coverage proving 500-when-403-expected fails the run via FuzzRunner (not just a static FuzzResult assertion)

- [ ] #84 — e2e-15 — HA storage chaos scenarios
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/84
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Git/API push under degraded topology (e.g. stop non-primary node, quorum push still succeeds) using IGitOperations + chaos helpers
    - Committed baselines for HaSmokeTests and any additional HA chaos scenarios (operations + git/API artifacts)
    - Shell script parity paths: RF=3 bare-repo verification, watermark increment on ≥2 nodes, clone/fetch read routing, promotion/resumed push (per retired test-ha-storage-e2e.sh manual steps and pop-13…

- [ ] #85 — e2e-16 — Merge request E2E scenarios
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/85
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Integrated MR lifecycle E2E: Draft → publish → approve → squash merge → Merged, with committed baselines/transcript
    - E2E asserting closes discussion link resolves linked discussion on merge
    - Private-repo MR auth matrix spot-checks (anonymous 404, outsider 403)

- [ ] #86 — e2e-18 — Discussion E2E scenarios
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/86
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Commit baselines + AssertBaselinesAsync() for all DiscussionSmokeTests scenarios (reopen, block/unblock, notifications, email side-channel, tags, anchors)
    - Assert reopen returns Open (status:0) and hasEverBeenEngaged:true (no re-Engage)
    - Assert blocked user can read discussions but cannot create/comment

- [ ] #87 — e2e-19 — Repository browse E2E scenarios
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/87
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Outsider 403 on private browse endpoints (tests currently codify 404)
    - Empty-repo tree empty-state assertion
    - Committed + asserted baselines for BrowseSmokeTests (cache headers, readme, empty refs, raw blob)

- [ ] #88 — e2e-20 — Runner documentation + shell script retirement
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/88
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Add tier model + fail-fast failure semantics to developer docs (tests/OpenGitBase.E2E/README.md or linked doc).
    - Update stale docs/issues/* run instructions (mr-16, disc-10, repo-browse-11) to runner commands.
    - Optional hygiene: .agents/testing.md and .agents/skills/prd-issues-tdd-local-main/compose-verification.md still list deleted discussion/repo-browse/HA scripts.

## E2E test population

_30 open_

- [ ] #89 — pop-01 — Scenario catalog + authoring checklist — **CLOSE-CANDIDATE**
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/89
  - Verdict: `complete` · confidence: `high`
  - Why open / gaps:
    - Audit marked complete but discussion still open — confirm close vs residual follow-ups

- [ ] #90 — pop-02 — Shared fixture library — **CLOSE-CANDIDATE**
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/90
  - Verdict: `complete` · confidence: `high`
  - Why open / gaps:
    - Audit marked complete but discussion still open — confirm close vs residual follow-ups

- [ ] #91 — pop-03 — Git testdata provisioning — **CLOSE-CANDIDATE**
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/91
  - Verdict: `complete` · confidence: `high`
  - Why open / gaps:
    - Audit marked complete but discussion still open — confirm close vs residual follow-ups

- [ ] #92 — pop-04 — Auth matrix theory runner
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/92
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Theory test ≥10 matrix rows with baselines
    - Skip/N/A handling documented

- [ ] #93 — pop-05 — Runner tag and feature filters — **CLOSE-CANDIDATE**
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/93
  - Verdict: `complete` · confidence: `high`
  - Why open / gaps:
    - Audit marked complete but discussion still open — confirm close vs residual follow-ups

- [ ] #94 — pop-06 — Report feature rollup dashboard — **CLOSE-CANDIDATE**
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/94
  - Verdict: `complete` · confidence: `high`
  - Why open / gaps:
    - Audit marked complete but discussion still open — confirm close vs residual follow-ups

- [ ] #95 — pop-07 — Integration test promotion indexer
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/95
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - At least one candidate manually promoted to demonstrate workflow

- [ ] #96 — pop-08 — Full-HA tier gating — **CLOSE-CANDIDATE**
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/96
  - Verdict: `complete` · confidence: `high`
  - Why open / gaps:
    - Audit marked complete but discussion still open — confirm close vs residual follow-ups

- [ ] #97 — pop-09 — F05 browse parity smoke
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/97
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - 5+ additional [Trait("Tag", "Smoke")] browse scenarios: public refs/tree anon 200, private auth matrix (anon 404 / outsider 403 / member 200), sub-1MB inline blob, isTooLarge, SVG classification
    - Smoke-tag existing BrowseE2eTests parity methods or migrate into BrowseSmokeTests
    - Committed baselines + AssertBaselinesAsync for all browse smoke scenarios

- [ ] #98 — pop-10 — F07 merge request parity smoke
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/98
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - @Smoke implementations for mr-16 scenarios 2 (full: feature-branch push allowed), 5 (draft→publish→approve→squash merge→merged), and 6 (closes link resolves discussion on merge).
    - Five+ additional @Smoke scenarios from the slice minimum list: unprotected direct push; public anon read MR + anon create 401; private anon 404 / outsider 403; push-rule rejection with message subs…
    - Catalog rows E2E-F07-002 … E2E-F07-010 with done status in docs/e2e/scenario-catalog.md.

- [ ] #99 — pop-11 — F06 discussion parity smoke
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/99
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Add catalog rows E2E-F06-004 … E2E-F06-010 mapping to DiscussionSmokeTests methods (and ensure legacy three rows reference Smoke trait consistently).
    - Run --update-baselines and commit baselines for all six DiscussionSmokeTests scenarios plus the email side-channel capture.
    - Add AssertBaselinesAsync() to each DiscussionSmokeTests method (matching DiscussionE2eTests pattern).

- [ ] #100 — pop-12 — F08 git HTTPS smoke expansion
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/100
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Org-owned repo HTTPS push (OrganizationFixture + PAT push/clone)
    - Protected-branch push denied as F08 smoke (coordinate with MR fixture per slice notes)
    - git fetch after push with ref assertion (GitOperations.FetchAsync needed)

- [ ] #101 — pop-13 — F10 HA parity smoke
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/101
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - E2E smoke: verify bare repos exist on storage-1, storage-2, storage-3 after repository create
    - E2E smoke: PAT push then assert replication watermarks on ≥2 nodes (storage health probes or admin replication API)
    - E2E smoke: stop non-primary storage node, push still succeeds (quorum 2/3)

- [ ] #102 — pop-14 — F01 auth smoke pack
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/102
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Run compose --update-baselines for all AuthSmokeTests methods and commit under Baselines/Auth/AuthSmokeTests/.
    - Add catalog rows E2E-F01-003 … E2E-F01-010 (or equivalent) and mark done.
    - Implement missing smoke scenarios: login-unverified-403, change-password-wrong-current-400, sign-out-then-401.

- [ ] #103 — pop-15 — F01 auth regression matrix
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/103
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Catalog update: add ≥50 F01 @Regression rows (E2E-POP15-*) marked done in docs/e2e/scenario-catalog.md
    - Committed email HTML baselines for reset and invite templates (verify exists; reset/invite absent)
    - Lifecycle scenarios from spec: account deletion → login denied; invite accept/decline via token; login rate-limit smoke (429, @Slow)

- [ ] #104 — pop-16 — F02 org + F04 members smoke — **HUMAN-DECISION**
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/104
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - 2 additional @Smoke scenarios to reach 20 (F02 needs ≥2 more; several F02/F04 required scenarios still absent)
    - F02: owner promote member, org invite create+accept, member cannot delete org, last owner cannot leave
    - F04: reader browse private repo, reader PAT clone, admin role assignment, last-admin demote guard, self-remove

- [ ] #105 — pop-17 — F02 org + F04 members regression
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/105
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - F02 catalog ≥50 regression rows done
    - F04 catalog ≥50 regression rows done
    - Auth matrix theories per feature

- [ ] #106 — pop-18 — F03 repository settings smoke
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/106
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - 10 smoke scenarios with baselines
    - Protected branch rule usable by MR tests

- [ ] #107 — pop-19 — F03 repository settings regression
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/107
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Update docs/e2e/scenario-catalog.md with ≥50 F03 regression rows marked done (or coverage summary reflecting POP19 matrix completion)
    - Git HTTPS E2E scenarios proving push-rule enforcement (forbidden paths, DCO) with denial message substring — reuse PatFixture + GitOperations as noted in slice
    - Protected-branch variant scenarios beyond basic blockDirectPush (require approvals, admin allowlist direct push)

- [ ] #108 — pop-20 — F05 browse regression matrix
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/108
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Populate docs/e2e/scenario-catalog.md with ≥50 F05 @Regression rows marked done (including E2E-POP20-* IDs per catalog convention)
    - Add rate-limit 429 scenario (@Slow) or explicit deferral note in docs/e2e/scenario-catalog.md for repo-browse-11 item 9
    - Ref-not-found 404 matrix rows (only missing-ref 400 present: tree-missing-ref, blob missing ref/path)

- [ ] #109 — pop-21 — F06 discussion regression
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/109
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Catalog update: mark ≥50 F06 regression rows done (matrix E2E-POP21-001..066 plus smoke/regression method rows).
    - ≥5 sub-thread E2E scenarios with committed baselines (create reply, nested reply, resolve/unresolve, permission matrix).
    - ≥2 additional notification side-channel baselines (mention-triggered, subscription opt-out, or sub-thread-resolve).

- [ ] #110 — pop-22 — F07 merge request regression
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/110
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Update docs/e2e/scenario-catalog.md with ≥50 F07 regression rows marked done (including E2E-POP22-* matrix IDs).
    - E2E scenarios for mr-16 #1, #3, #4, #5, #7, #9, #10.
    - Git + API baselines for squash-merge and conflict/mergeability-disabled paths.

- [ ] #111 — pop-23 — F08 git HTTPS regression
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/111
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Update docs/e2e/scenario-catalog.md with ≥50 F08 @Regression rows marked done (including E2E-POP23-* matrix IDs per catalog convention).
    - Implement git-level regression scenarios stubbed as N/A: 10 transport probes in GitHttpsRegressionMatrix (lines 132–146) skip clone/push/role-matrix git ops with SkipReason: "Transport-level clone/…
    - Slice "What to build" gaps: wrong repo slug on git remote, push/fetch tags, large push smoke, PAT expiry edge, access-check API full role matrix, org-repo HTTPS (see scripts/integration-test-https-…

- [ ] #112 — pop-24 — F12 discovery + notifications smoke
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/112
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Commit baseline bundles for all 10 DiscoverySmokeTests methods (--update-baselines + review)
    - Add/catalog missing smoke scenarios: explore/API consistency (or document pop-29 deferral), new-user empty notification list, MR notification fan-out, explicit discussion notification smoke with F0…
    - Add E2E-F12-001 … E2E-F12-010 rows to docs/e2e/scenario-catalog.md; update F12 coverage summary to 10/10 smoke

- [ ] #113 — pop-25 — F12 discovery + notifications regression
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/113
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Update docs/e2e/scenario-catalog.md with ≥50 E2E-POP25-* rows marked done (catalog is the acceptance gate per slice)
    - Add ≥5 cross-feature notification scenarios verifying F06 comment and F07 merge produce inspectable notification records (slice “What to build” + story 114)
    - Implement remaining scope from slice body: discovery pagination/filtering depth, owner profile variants (empty / many repos), notification fan-out (MR merge, mention, subscribe/unsubscribe), mark-a…

- [ ] #114 — pop-26 — F10 HA full regression
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/114
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Expand F10 scenario catalog to ≥50 @Regression @FullHa rows marked done (currently 1/52 target).
    - Add ≥15 more regression matrix cases or dedicated scenario tests to reach ≥50 @FullHa @Regression rows (currently 35 in HaRegressionMatrixTests).
    - Implement ha-storage-12 E2E depth: primary stop → promotion → push resumes, RF=3 bare-repo verification, watermark quorum push, read-replica routing metadata, quorum push with non-primary down, del…

- [ ] #115 — pop-27 — F11 admin fleet smoke + regression
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/115
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - AdminSmokeTests with 10 smoke scenarios (admin login + list nodes, replication summary after repo create, per-repo detail, enrollment smoke, healthy node count, degraded flag via chaos, fleet SSH k…
    - ~22+ additional @Regression matrix/functional cases to reach ≥50 (replication after push/quorum, attention/severity list API, multi-repo summary, full admin route auth matrix including replication …
    - F11 catalog rows in docs/e2e/scenario-catalog.md updated to done with E2E-F11-* / E2E-POP27-* IDs

- [ ] #116 — pop-28 — F09 SSH git profile scenarios
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/116
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - ≥50 @Regression scenarios (only 35 matrix cases in GitSshRegressionMatrixTests, no GitSshSmokeTests)
    - 10 smoke scenarios: SSH key create, fingerprint lookup, clone/push over SSH, unauthorized/revoked key rejection, reader/outsider access, protected-branch push denial, profile skip message
    - Catalog rows with done status for all implemented scenarios

- [ ] #117 — pop-29 — Playwright behavioral regression specs
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/117
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Wire tests/behavioral/ into tier 8 (PlaywrightInvoker or unified config) so all @regression specs run together
    - Add catalog rows for Playwright UI scenarios mapped to F01, F05, F06, F07, F11, F12
    - Document visual (pixel snapshot) vs behavioral (MSW + assertions) split in web README; note complement to C# E2E

- [ ] #118 — pop-30 — CI smoke vs regression documentation
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/118
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Human sign-off: reviewer name and date in the Review sign-off table (docs/e2e/ci-strategy.md)
    - HITL review completed (name/date in doc)

## Encrypted replica storage

_15 open_

- [ ] #121 — ers-03 — Storage artifact API and encrypted node isolation
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/121
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Atomic/durable artifact PUT (temp write + rename and/or fsync before 201)
    - Explicit Smart HTTP and SSH rejection of upload-pack/receive-pack when repo role is EncryptedReplica
    - Encrypted-role guard on all /internal/repos/content/* handlers (not only the unreachable fallback branch)

- [ ] #122 — ers-04 — Four-copy repository create
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/122
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Compose smoke test (or E2E) that creates a repository on the default three-node fleet and asserts four-copy layout (primary+read on storage-1, encrypted on storage-2/storage-3, Rf4Healthy state)
    - Handler test assertion for PrimaryWatermark == 0 and explicit key-generation call verification
    - Clarify whether acceptance criterion expects 4 DB rows when read replica colocates with primary (implementation intentionally persists 3 rows + ReadReplicaStorageNodeId metadata)

- [ ] #123 — ers-05 — Encrypted quorum push
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/123
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Integration test asserting corrupt/tampered encrypted artifact is rejected during quorum confirmation (not just missing artifact)
    - Integration test asserting read-replica git sync and second encrypted-replica upload happen asynchronously after quorum commit without blocking the handler response
    - Integration tests cover happy path, encrypted node down, corrupt upload rejection, and async catch-up

- [ ] #124 — ers-06 — Read/write routing split
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/124
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Add GitRoutingTargetSelectorTests.SelectReadTarget_FallsBackToPrimaryWhenReadTargetsEmpty (or equivalent access-check controller test with RF4 primary+read replica targets asserting read routes to …
    - Add RepositoryAccessChecksControllerTests asserting ReadReplica is populated separately from Primary on RF4 routing responses
    - Optional: integration/parity test proving HTTPS and SSH both honor the same ReadTargets ordering

- [ ] #125 — ers-07 — Hot promotion and cold recovery
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/125
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Dedicated ColdRecoveryService tests (happy path, corrupt artifact abort, interrupted recovery)
    - Integration test wiring cold-recovery trigger from PromotePrimaryReplicaQueryHandler when both plaintext copies are lost
    - Ref manifest comparison / git bundle verify parity check after decrypt (spec calls for bundle verify + ref manifest comparison; only envelope decrypt/verify present)

- [ ] #126 — ers-08 — RF=3 to RF=4 background backfill
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/126
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Produce and confirm initial encrypted bundle from primary before Rf4Healthy transition
    - Watermark-based read-replica designation (highest-watermark non-primary) vs planner-only selection
    - Retry/resume path for repos stuck in Rf4Migrating (backoff loop)

- [ ] #127 — ers-09 — Delete, rebalance, and anti-entropy extensions
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/127
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - RF4 service tests: delete with one node down (3/4 quorum), encrypted replica rebalance with artifact backfill, read-replica lag repair, encrypted artifact repair
    - Heartbeat artifact watermark reporting (ArtifactWatermark in heartbeat payload and ApplyRepositoryWatermarksQueryHandler)
    - Orphaned artifact directory cleanup (mentioned in slice "What to build" but absent from AntiEntropyReconcilerService)

- [ ] #128 — ers-10 — Admin UI four-copy replication status
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/128
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - List and detail API tests for Rf4Migrating repositories (explicit acceptance criterion)
    - List API tests for RF=4 healthy summary responses (only detail test exists today)
    - RF=4 attention semantics: ReplicationAttention.NeedsAttention treats only Rf3Healthy as healthy (flags Rf4Healthy repos as needing attention); list MaxWatermarkLag ignores encrypted ArtifactWaterma…

- [ ] #129 — ers-11 — Phase 1 E2E and integration tests
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/129
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Cold recovery producing identical refs
    - AEAD tamper rejection on recovery
    - E2E/compose chaos: primary failure + surviving read replica

- [ ] #130 — ers-12 — Org storage node registration and capacity
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/130
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Registered org node in fleet with OwnerOrganizationId
    - Platform admin can set MaxBytes
    - Tests cover org registration, capacity rejection, platform admin capacity

- [ ] #131 — ers-13 — Self-host tier placement
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/131
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Org with 3 nodes: fully self-hosted four-copy repo
    - Per-repo override supersedes org default
    - Planner unit tests: all tiers, colocation, insufficient rejection

- [ ] #132 — ers-14 — Org quota credits and placement settings UI
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/132
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Repo settings show inherited placement policy with override
    - Admin can view platform node capacity configuration
    - Tests cover quota, inheritance, override persistence

- [ ] #133 — ers-15 — Cross-org encrypted placement algorithm
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/133
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Integration test: encrypted copies on second org's node in two-org compose/dev setup

- [ ] #134 — ers-16 — Per-repo byte overrides and capacity engine — **CLOSE-CANDIDATE**
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/134
  - Verdict: `complete` · confidence: `high`
  - Why open / gaps:
    - Audit marked complete but discussion still open — confirm close vs residual follow-ups

- [ ] #135 — ers-17 — Org node capacity shrink via platform rebalance — **BLOCKED** · **HUMAN-DECISION**
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/135
  - Verdict: `blocked` · confidence: `high`
  - Why open / gaps:
    - Design doc or ADR captures shrink/drain workflow and quota timing
    - Org owner can shrink MaxBytes after repos are moved off the node
    - Platform rejects shrink when repos still assigned and used bytes exceed new max

## Git HTTPS / PAT

_6 open_

- [ ] #136 — git-https-01 — Git access tokens + settings UI + git config — **CLOSE-CANDIDATE**
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/136
  - Verdict: `complete` · confidence: `high`
  - Why open / gaps:
    - run pnpm sync:api to refresh swagger.

- [ ] #138 — git-https-03 — Storage git-http-backend — **CLOSE-CANDIDATE**
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/138
  - Verdict: `complete` · confidence: `high`
  - Why open / gaps:
    - Audit marked complete but discussion still open — confirm close vs residual follow-ups

- [ ] #139 — git-https-04 — Dispatcher Smart HTTP edge — **CLOSE-CANDIDATE**
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/139
  - Verdict: `complete` · confidence: `medium`
  - Why open / gaps:
    - Manual clone/push via dispatcher port + PAT

- [ ] #141 — git-https-06 — SSH disable gate
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/141
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Add GIT_SSH_ENABLED: "true" to api-1/api-2 in docker-compose.ssh.yml (or fix README) so profile restore also re-enables SSH keys UI via git/config.
    - Optional: compose E2E or manual verification record for ssh://git@localhost:2211/... with --profile ssh.
    - GIT_SSH_ENABLED=true + --profile ssh restores full SSH git path

- [ ] #142 — git-https-07 — Repository HTTPS clone URLs + settings navigation
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/142
  - Verdict: `partial` · confidence: `medium`
  - Why open / gaps:
    - Repo overview displays https://opengitbase.com/{owner}/{repo}.git when API config set
    - Fallback strips www. from window.location.origin when API unavailable

- [ ] #143 — git-https-08 — End-to-end HTTPS git integration test — **CLOSE-CANDIDATE**
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/143
  - Verdict: `complete` · confidence: `high`
  - Why open / gaps:
    - Audit marked complete but discussion still open — confirm close vs residual follow-ups

## Git storage proxy & multi-node

_5 open_

- [ ] #144 — 01-storage-node-registry-and-heartbeat — Storage node registry and heartbeat — **CLOSE-CANDIDATE**
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/144
  - Verdict: `complete` · confidence: `high`
  - Why open / gaps:
    - Audit marked complete but discussion still open — confirm close vs residual follow-ups

- [ ] #145 — 02-storage-internal-http-lifecycle-api — Storage internal HTTP lifecycle API
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/145
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Storage-layer integration test verifies provision + delete over HTTP with token auth
    - Provisioned bare repo usable by git-upload-pack / git-receive-pack on storage

- [ ] #146 — 03-repository-create-delete-with-storage-assignment — Repository create/delete with storage assignment
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/146
  - Verdict: `partial` · confidence: `medium`
  - Why open / gaps:
    - Create selects healthy node with most free disk
    - Delete removes bare repo synchronously before DB
    - Delete fail-fast on missing/unhealthy node

- [ ] #147 — 04-dispatcher-git-proxy — Dispatcher git proxy (read + write)
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/147
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Manual verification (clone/pull/push @ :2223)

- [ ] #148 — 05-network-isolation-and-e2e-integration-tests — Network isolation and end-to-end integration tests
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/148
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - E2E test: register storage → API create repo → push → clone via dispatcher
    - Integration test in CI or documented pre-merge check
    - docker compose up default starts full stack without manual steps

## HA storage replication (RF=3)

_12 open_

- [ ] #149 — ha-storage-01 — Three-node fleet foundation — **CLOSE-CANDIDATE**
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/149
  - Verdict: `complete` · confidence: `high`
  - Why open / gaps:
    - Audit marked complete but discussion still open — confirm close vs residual follow-ups

- [ ] #150 — ha-storage-02 — Replica set schema and quorum create
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/150
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Three RepositoryReplica rows: one primary + two replicas @ watermark 0
    - Storage integration or compose smoke: bare repos on all three nodes

- [ ] #151 — ha-storage-03 — Storage peer mTLS replication — **CLOSE-CANDIDATE**
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/151
  - Verdict: `complete` · confidence: `high`
  - Why open / gaps:
    - Audit marked complete but discussion still open — confirm close vs residual follow-ups

- [ ] #152 — ha-storage-04 — Quorum push and watermark commit
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/152
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Integration test: push through dispatcher updates watermarks on two nodes; third may lag
    - Integration test: push with two nodes down fails cleanly

- [ ] #153 — ha-storage-05 — Read/write routing (access check + dispatcher)
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/153
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - RepositoryAccessChecksControllerTests cases asserting: allowed response populates Primary, ReadTargets, ReplicationEpoch; lagging replica (IsInSync=false) excluded from ReadTargets; WriteGit denied…
    - Git-level test (compose or integration) proving post-quorum-push fetch succeeds via both primary and in-sync replica paths.
    - Client fetching after a quorum push sees pushed commits whether hitting primary or in-sync replica

- [ ] #154 — ha-storage-06 — Primary failover and epoch promotion
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/154
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Unhealthy primary triggers promotion
    - Access-check returns new primary without dispatcher restart
    - Clone/fetch via new primary or in-sync replica

- [ ] #155 — ha-storage-07 — Quorum delete and async third scrub
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/155
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Third node scrubbed asynchronously after DB removal
    - Handler tests: happy path, one-node-down, quorum failure
    - Integration test: bare repo absent on 2 nodes, eventually on 3rd

- [ ] #156 — ha-storage-08 — RF=1 → RF=3 background backfill
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/156
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Two additional replicas provisioned via git-native peer sync
    - Git clone/fetch/push on original primary continues during backfill
    - After completion: 3 RepositoryReplica rows with matching watermarks

- [ ] #157 — ha-storage-09 — Automatic rebalance and reattach
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/157
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Unhealthy node beyond threshold enqueues rebalance/backfill for affected repos
    - Recovered node after replacement in-sync: replacement kept, recovered node marked spare

- [ ] #158 — ha-storage-10 — Anti-entropy reconciler
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/158
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Detect missing bare repo on trio member → enqueue backfill
    - Detect on-disk repos without DB records → scrub orphans
    - Re-enqueue stalled jobs for Degraded / long-running RF1Backfilling

- [ ] #159 — ha-storage-11 — Admin UI replication status
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/159
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Admin API per-repository replication detail
    - API tests with seeded replica sets

- [ ] #160 — ha-storage-12 — End-to-end HA integration tests
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/160
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - E2E creates repo; bare repos on 3 storage nodes
    - Push commits; watermarks on ≥2 nodes
    - Clone/fetch via non-primary in-sync replica

## Merge requests

_13 open_

- [ ] #161 — mr-01 — Merge request authorization — **HUMAN-DECISION**
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/161
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Controller integration test: List_PrivateRepositoryOwner_ReturnsOk (owner authenticated, no member record)
    - Optional hardening: explicit Get_PublicRepositoryAnonymous_ReturnsOk and Approve_AnonymousOnPublic_ReturnsUnauthorized controller tests (behavior implemented; covered indirectly today)
    - API controller integration tests assert status codes for anonymous, outsider, member, and owner on list endpoint

- [ ] #162 — mr-02 — Default branch persistence and settings
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/162
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Unit or integration test that first GET …/content/refs persists inferred DefaultBranchName when null
    - API controller test for PATCH …/settings/default-branch (success + 400 on unknown branch + 403 for non-admin)
    - Optional follow-up in mr-06: regression test that changing default branch does not mutate existing MR TargetRef

- [ ] #163 — mr-03 — Protected branch and push rule CRUD
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/163
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Regenerate and commit OpenAPI spec (node scripts/sync-openapi.mjs or pnpm sync:api) so swagger.json documents protected-branch-rules CRUD endpoints and request/response schemas.
    - OpenAPI documents settings endpoints

- [ ] #164 — mr-04 — Git push enforcement
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/164
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Paired HTTPS access-check test mirroring SSH protected-main deny scenario
    - E2E step asserting writer can still push to a feature branch after main is protected (scenario 2 in mr-16)
    - HTTPS and SSH access-check paths behave identically in tests

- [ ] #167 — mr-07 — Approvals and merge gates
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/167
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - MR detail Web UI: approve button (eligible users), approval count widget (e.g. “1 of 2 required”), wire to POST .../approve
    - Web API client: extend MergeRequest normalization with approval fields and add mergeRequests.approve
    - Handler tests: draft approve rejection; refresh-SHA path with DismissApprovalsOnPush reverting Approved→Open

- [ ] #168 — mr-08 — Server-side merge and discussion closes links
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/168
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Web UI merge dialog (strategy picker, delete-source-branch checkbox), merge button gating, and mergeability banner on MR detail page — applications/opengitbase-web/app/utils/api.ts has no merge/get…
    - E2E/API happy-path merge test verifying commit lands on target and MR → Merged
    - E2E test: closes link resolves discussion on merge; related/implements unchanged

- [ ] #170 — mr-10 — Overview comments
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/170
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - merge_request_comments table + EF entity/configuration (absent from OpenGitBaseDbContextModelSnapshot.cs)
    - Backend CRUD query handlers and contracts under features/merge-request/
    - API routes: GET/POST …/comments, PATCH/DELETE …/comments/{id} with Reader+ participate / Writer+ delete-any auth (wire MergeRequestAuthorizationService)

- [ ] #171 — mr-11 — Changes tab, diff, and review threads
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/171
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Backend merge_request_comments storage (table/entity) and CQRS handlers
    - GET/POST/PATCH/DELETE .../merge-requests/{number}/comments plus .../resolve and .../unresolve endpoints
    - Review-comment anchor model (headCommitSha, filePath, lineNumber, diffSide) persisted server-side

- [ ] #172 — mr-12 — Branches and push rules settings UI — **CLOSE-CANDIDATE**
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/172
  - Verdict: `complete` · confidence: `medium`
  - Why open / gaps:
    - .

- [ ] #173 — mr-13 — Post-push create banner
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/173
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Fix web API client: change branch-ahead-summary?ref= to ?refName= in applications/opengitbase-web/app/utils/api.ts (CLI and E2E already use refName)
    - Gate banner visibility on push/write access (acceptance criterion says "authenticated pusher"; behavior section marks read-only hint as optional)
    - Add API test(s) for BranchAheadSummary edge cases: empty repo, no default branch, active MR present, ref not ahead

- [ ] #174 — mr-14 — Merge request notifications
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/174
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Schema migration: nullable mergeRequestId on user_notifications (and optional nullable DiscussionId)
    - Backend NotificationEventType values for MR events (frontend enum mapping exists in api.ts but backend enum stops at SubThreadResolved)
    - CreateMergeRequestNotificationQuery (or extend discussion handler) with MR subscription rules (author, commenters, approvers)

- [ ] #175 — mr-15 — Linked discussions sidebar
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/175
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Backend GET /repository/by-slug/{owner}/{slug}/discussions/{number}/linked-merge-requests endpoint (query handler + controller action) so discussion detail can populate linked MRs
    - Optional: visual/E2E test for discussion detail linked-MR sidebar once API exists
    - Optional: E2E asserting merge resolves a discussion linked via POST discussion-links with closes

- [ ] #176 — mr-16 — End-to-end merge request integration tests
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/176
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - End-to-end scenario 5: Draft → publish Open → approvals → Approved → squash merge → Merged
    - End-to-end scenario 6: closes discussion link resolves discussion status on merge
    - Private-repo MR auth matrix (anon 404, outsider 403)

## Public status dashboard

_6 open_

- [ ] #178 — status-02 — Web and git dispatcher health and registration
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/178
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Automated tests for dispatcher and web /health returning 200
    - Integration or compose test verifying fleet registry lists web-1, web-2, dispatcher-1, dispatcher-2 after stack boot
    - Test coverage for Website component registration (distinct from Git/API handler tests)

- [ ] #179 — status-03 — Status rollup and probe engine
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/179
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Unit test for ProbeTcpAsync (success, slow, failure, timeout) with mocked TCP
    - Unit test for HTTP probe timeout → Unhealthy
    - Fix or test: null LastHeartbeatAt should be Unhealthy per PRD normative table, not Degraded

- [ ] #180 — status-04 — Background aggregator and public status API
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/180
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Unit or integration tests for advisory-lock exclusivity (two concurrent acquirers)
    - Test that aggregator tick persists expected snapshot to StatusSnapshotEntity
    - Test that public status JSON omits internal fields (host, port, cert thumbprint, enrollment token, disk bytes)

- [ ] #181 — status-05 — Hourly history aggregation and public history API
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/181
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Unit/integration tests for hourly bucket upsert and same-hour idempotency (UpsertBucketAsync)
    - Unit/integration tests for 90-day retention prune (PruneOldBucketsAsync)
    - Optional: explicit test that GetHistoryAsync returns fewer than requested days when data is young

- [ ] #184 — status-08 — Admin incident banner
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/184
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - API tests for admin incident endpoints: set, replace (second set deactivates first), resolve, and non-admin/anon rejection
    - Optional: handler unit tests for SetStatusIncidentBannerQueryHandler / ResolveStatusIncidentBannerQueryHandler
    - API tests cover set, replace, resolve, and auth

- [ ] #185 — status-09 — E2E smoke and cross-surface polish
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/185
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Regenerate and commit applications/opengitbase-web/openapi/swagger.json (via pnpm sync:api / swagger export) so public status, history, and admin incident endpoints appear in the OpenAPI artifact
    - Add Playwright footer-link navigation smoke or document a manual smoke checklist (footer → /status, status page render) in slice notes or docs/e2e/scenario-catalog.md
    - Optional follow-up: register PublicStatusSmokeTests in docs/e2e/scenario-catalog.md; add incident-banner Playwright smoke (recommended, not minimum bar)

## Discussions & sub-threads

_15 open_

- [ ] #186 — disc-01 — Discussion authorization
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/186
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - RepositoryDiscussionsControllerTests list-endpoint auth matrix (anonymous / outsider / member / owner) — explicit AC, mirrored in MR controller tests but not discussions
    - DiscussionAuthorizationServiceTests private-repo participate scenarios (outsider → Forbidden, member/owner → Allowed)
    - Org-member read access test for org-owned private repo discussions

- [ ] #188 — disc-03 — Discussion detail, assignee, and Writer close actions
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/188
  - Verdict: `partial` · confidence: `medium`
  - Why open / gaps:
    - Assignee repo-member validation on create/update API
    - Assignee picker on detail page and create form (DiscussionCreateDrawer.vue has no assignee field)
    - Title/assignee edit UI wired to PATCH /discussions/{number}

- [ ] #189 — disc-04 — Thread comments and engagement lifecycle
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/189
  - Verdict: `partial` · confidence: `medium`
  - Why open / gaps:
    - Wire discussion comment edit and soft-delete in web UI (api.discussions.updateComment / deleteComment, hooks in useDiscussionDetailPage, edit/delete controls + edited indicator in thread UI)
    - Replace placeholder unit tests for update/delete handlers with real coverage (author edit, author delete, writer+ moderation delete, DB row retention)
    - Add explicit tests for second top-level non-creator comment and reopened-discussion no-re-Engage (top-level path)

- [ ] #190 — disc-05 — Repository tags
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/190
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Repository settings tag management section for Admin/Owner (create/rename/delete catalog)
    - Tag picker on edit discussion form (create drawer has picker; detail page shows tags read-only)
    - Tags displayed on discussion list rows (detail header only)

- [ ] #191 — disc-06 — Blocked users (participation controls)
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/191
  - Verdict: `partial` · confidence: `medium`
  - Why open / gaps:
    - Flesh out RepositoryBlockedUsersControllerTests (list/block/unblock + role matrix)
    - Replace placeholder query-handler tests with real DB-backed assertions
    - E2E: blocked user cannot create discussion; blocked user can still GET list/detail while blocked

- [ ] #192 — disc-07 — Mentions, subscriptions, and in-app notifications
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/192
  - Verdict: `partial` · confidence: `medium`
  - Why open / gaps:
    - Server-side mention resolution filtered to repo readers (strip/ignore users without read access)
    - Mention parsing on comment edit (UpdateDiscussionCommentQueryHandler does not re-parse mentions)
    - Mark-all-read API/UI

- [ ] #193 — disc-08 — Email notifications
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/193
  - Verdict: `partial` · confidence: `medium`
  - Why open / gaps:
    - SendGrid via existing infra; failures logged; POST not failed
    - Unit/integration test with SendGrid double/fixture for subject

- [ ] #194 — disc-09 — Anchored code comments
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/194
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Create anchored comment stores ref, commitSha, path, line
    - Tests: anchor persistence; resolver smoke with fixture repo

- [ ] #195 — disc-10 — End-to-end discussion integration tests
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/195
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Public anonymous read
    - Engaged once + reopen without re-Engage
    - Block mute enforced; unblock restores write

- [ ] #196 — disc-11 — Basic sub-thread replies
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/196
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Author edit and soft-delete on replies
    - Writer+ soft-delete on replies
    - API tests for nesting, validation, lifecycle matrix

- [ ] #197 — disc-12 — Anchored replies
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/197
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - POST reply with anchor stores ref, commitSha, path, line
    - GET nested list includes anchor on replies; resolver runs on read
    - Anchored reply may reference different file/line than root

- [ ] #198 — disc-13 — Sub-thread resolve and collapse UI
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/198
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Resolve/unresolve on reply → 400
    - Resolved badge persists after new reply
    - Discussion updatedAt bumps on resolve/unresolve

- [ ] #199 — disc-14 — Orphan replies after root soft-delete — **CLOSE-CANDIDATE**
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/199
  - Verdict: `complete` · confidence: `high`
  - Why open / gaps:
    - Audit marked complete but discussion still open — confirm close vs residual follow-ups

- [ ] #200 — disc-15 — Sub-thread resolve notifications
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/200
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - API/handler tests for scoped fan-out on resolve
    - Email test for subject/body distinction (disc-08 path)

- [ ] #201 — disc-16 — Sub-thread integration tests
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/201
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - HTTP-level sub-thread integration suite extending the disc-10 Compose harness (DiscussionE2eTests / DiscussionSmokeTests or dedicated DiscussionSubThreadE2eTests)
    - E2E scenarios for all PRD integration paths listed in the slice (nested list with 2 replies, reply-to-reply 400, Writer+ resolve, resolve+reply on resolved sub-thread, blocked reply, scoped resolve…
    - Regression matrix rows / baselines for sub-thread endpoints (planned in pop-21, not yet present)

## Repository web browsing

_11 open_

- [ ] #202 — repo-browse-01 — Storage content HTTP API
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/202
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Remove or raise the 1 MB cap on the raw endpoint so downloads work for blobs of any size (per slice + PRD)
    - Add tests asserting isBinary/size on blob responses (text + binary fixture)
    - Add test for readme 404 when no candidate exists at repo root

- [ ] #203 — repo-browse-02 — Public root tree in the web UI
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/203
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - MSW handler for GET /api/repository/by-slug/:owner/:slug/content/tree (and optionally readme) so visual/behavioral tests assert rendered entries
    - Controller-level tests for default-ref resolution and entry sort order (or update slice wording to accept service-level tests)
    - OpenAPI export (openapi/swagger.json) still lacks /content/* routes; web client is hand-written in api.ts rather than generated

- [ ] #204 — repo-browse-03 — Private repository content authorization
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/204
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Unit tests: AuthorizeReadAsync for repo reader and org-member on org-owned private repo.
    - Controller tests: private blob auth matrix (anon/outsider/member/owner) mirroring tree coverage.
    - Controller tests: private tree success paths for owner and repo reader.

- [ ] #205 — repo-browse-04 — Branch/tag ref picker and tree navigation
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/205
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - GetRefs unit tests in RepositoryContentControllerTests (public/private/authorized matrix)
    - UI automation: home → subfolder → switch branch → assert URL and directory content update
    - Ref-switch fallback: when path does not exist on new ref, navigate to /tree/{ref} root instead of showing pathNotFound

- [ ] #206 — repo-browse-05 — README on repository home
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/206
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - GetReadme controller unit tests (200 success + 404 when absent)
    - UI/component or Playwright test verifying rendered README heading on repository home
    - Optional hardening: explicit case-insensitive precedence unit test; markdown sanitizer unit tests for <script> / event-handler stripping

- [ ] #207 — repo-browse-06 — Blob view — text, download, size cap
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/207
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - RepositoryContentControllerTests for text blob, oversized metadata, and binary flag
    - At-cap vs over-cap unit test (size == 1_048_576 vs 1_048_577)
    - Playwright (or equivalent) tree → blob navigation test with content assertion

- [ ] #208 — repo-browse-07 — Blob preview — images, SVG, markdown toggle
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/208
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Image blob fixture (e.g. .png in GitTestDataLayout) with E2E or unit assertion for previewKind: "image" and optional contentBase64
    - GetBlob API controller tests asserting PreviewKind passthrough from storage payload
    - Python unit tests for _preview_kind covering image, svg, and binary paths (currently only text in test_storage_content.py)

- [ ] #209 — repo-browse-08 — Web replica routing and syncing banner
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/209
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - Retry/fallback to next healthy non-primary replica when the first selected replica is unreachable
    - Reject (503) instead of primary fallback when non-primary replicas exist but none are healthy
    - Controller integration test asserting primary is never used for content reads when a healthy non-primary exists

- [ ] #210 — repo-browse-09 — Redis cache, cache headers, anonymous rate limits
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/210
  - Verdict: `partial` · confidence: `medium`
  - Why open / gaps:
    - Unit tests for RepositoryContentCacheKeys.Build, RepositoryContentCacheTtl.Default (60s), and RepositoryContentCacheService get/set/TTL
    - API integration test proving cache hit avoids IStorageContentClient calls
    - API integration test proving anonymous content browse returns 429 after 120 req/min

- [ ] #211 — repo-browse-10 — Empty repository state and collapsible clone
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/211
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - E2E or API integration tests for empty-repo tree/readme content endpoints (assert non-500, e.g. 404 or empty payload)
    - Automated UI test (component or Playwright) asserting empty repo page lacks directory table and seeded repo page shows RepoDirectoryTable
    - Optional: MSW handlers for content/tree and content/readme to make repo-overview visual regression meaningful for browse layout

- [ ] #212 — repo-browse-11 — End-to-end repository browse integration tests
  - Forge: https://api.opengitbase.com/opengitbase/open-git-base/discussions/212
  - Verdict: `partial` · confidence: `high`
  - Why open / gaps:
    - scripts/test-repo-browse-e2e.sh (or complete e2e-20 shell retirement + doc updates per slice deliverable)
    - E2E integration asserting private outsider → 403 on content endpoints (fix matrix expectation E2E-POP20-004 if product intent is 403 per repo-browse-03)
    - Empty-repo assertion for empty branches / isEmpty:true (not just non-500)

## Closed slices (out of scope)

20 audited slices are no longer open and are **not** on this checklist:
#29, #35, #39, #66, #68, #69, #70, #71, #83, #119, #120, #137, #140, #165, #166, #169, #177, #182, #183, #187.

Re-list anytime with:

```bash
ogb --hostname https://api.opengitbase.com issue -R opengitbase/open-git-base list --status open
```
