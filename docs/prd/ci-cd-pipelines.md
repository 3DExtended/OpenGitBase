<!-- forge: #7 -->

# PRD: CI/CD Pipelines (Firecracker + Hybrid Compute)

**Status:** Design complete (full vision). Domain glossary: [CONTEXT.md](../../CONTEXT.md). ADR: [0001-pipeline-trigger-event-bus.md](../adr/0001-pipeline-trigger-event-bus.md). User docs: `/docs/ci/*` in the web application.

---

## Problem Statement

OpenGitBase is a self-hosted Git forge with organization-contributed **storage nodes**, web collaboration, and merge requests — but no way to run user-defined build and test workloads when code is pushed. Teams expect CI configuration in version control, isolated execution, and the option to use platform capacity or their own hardware.

The platform must balance:

- **Author flexibility** — many languages, package managers, and optional container tooling (Docker) without prebuilding every environment combination.
- **Operational control** — curated base filesystems, auditable dependency layers, egress allowlists, resource ceilings, and abuse resistance on shared infrastructure.
- **Hybrid compute** — platform-hosted runners for easy onboarding, org-self-hosted runners for compliance, and community-contributed runners for federated capacity (mirroring the storage-node contribution model).
- **Trust in caching** — promoting frequently used dependency installs to reusable OverlayFS layers must never publish layers built from failing install scripts.

Jobs must run in **Firecracker MicroVMs** (not container-per-job isolation), composed from a **Base Image** catalog entry plus optional **Dependency Layers**, on registered **Compute Nodes**, with per-job credentials distinct from node enrollment identity.

---

## Solution

Repositories define pipelines in **`.opengitbase-ci.yml`** at the repository root (v1). A git push triggers evaluation; matching jobs run in **Job Sandboxes** on **Compute Nodes** selected by each job's required **Hosting Profile** (`runs-on`).

### Author-facing configuration (v1)

```yaml
stages:
  - build
  - test

image: alpine:26.1.1

dependencies:
  - dotnet-sdk:
    - version: 11
    - installscript: |
        sudo apk add dotnet10-sdk
  - python:
    - version: 31
    - installscript: |
        sudo apk add python31

variables:
  GIT_DEPTH: 0

build:
  stage: build
  runs-on: ogb-hosted
  only:
    - main
    - release/*
  script: |
    dotnet build

test:
  stage: test
  runs-on: ogb-hosted
  only:
    - main
    - release/*
  script: |
    dotnet test
```

Key rules:

- Every job **must** declare `runs-on` (no default).
- `image:` is a **Base Image Catalog** slug (not `docker pull` at job time).
- `dependencies:` is ordered; each entry is a **Dependency Recipe** with optional promoted **Dependency Layer**.
- Top-level `image`, `dependencies`, and `variables` are **Pipeline Defaults** overridable per job.
- `only:` uses glob patterns against the pushed ref name (`*`, no `**` in v1).
- `installscript` runs as root; `script` runs as `ogb` by default; optional `user:` is `ogb` or `root`.
- Docker and other **Container Tooling** only when declared as a dependency.

### End-to-end flow

1. **Trigger** — primary storage `post-receive` calls a thin internal API (`repositoryId`, `ref`, `afterSha`). API publishes **Git Push Received** to Kafka (`git.push.received`). Missing `.opengitbase-ci.yml` at `afterSha` → silent no-op.
2. **Schedule** — pipeline scheduler consumer loads YAML, creates **Pipeline Run**, evaluates **Branch Filters**, orders **Stages**, enqueues **Jobs** (Postgres queue of record).
3. **Dispatch** — scheduler publishes `ci.job.available` to Kafka. **Platform Compute Node Agents** consume for wake; **Org Compute Node Agents** long-poll the claim API. All agents claim jobs via API and receive **Job Identity** + spec (no inbound agent connections).
4. **Prepare sandbox** — agent composes OverlayFS (base + dependency layers + ephemeral upper), boots Firecracker MicroVM, runs missing `installscript` blocks in order inside the guest.
5. **Materialize workspace** — host fetches repo with **Job Identity** (`GIT_DEPTH: 0` → full worktree mount; `GIT_DEPTH: N` → shallow archive) at `$CI_PROJECT_DIR`.
6. **Execute** — inject **CI Variables**, run `script`, stream logs, enforce **Egress Policy** and **Job Resource Limits**.
7. **Teardown** — destroy MicroVM, revoke **Job Identity**. Failed job fails **Stage**; in-flight siblings finish; later stages skipped.

### Hosting profiles

| `runs-on` | Compute pool |
|-----------|----------------|
| `ogb-hosted` | **Platform Compute Nodes** |
| `organization-self-hosted` | Owning org's `OwnOrgOnly` nodes |
| `community-hosted` | Any org's `CrossOrgAllowed` nodes |

### Infrastructure (v1)

| Component | Choice |
|-----------|--------|
| Event bus | 3-broker Apache Kafka (KRaft), RF=3 topics |
| Layer Store | S3-compatible (MinIO in compose) |
| Job queue | Postgres (authoritative state) |
| Isolation | Firecracker + OverlayFS per job |

---

## User Stories

### Pipeline configuration and authoring

1. As a pipeline author, I want to define CI in `.opengitbase-ci.yml` at the repository root, so that pipelines are versioned with code.
2. As a pipeline author, I want a published JSON Schema for editor validation, so that I catch mistakes before push.
3. As a pipeline author, I want optional `stages:` with first-seen fallback ordering, so that I can control stage sequence without boilerplate.
4. As a pipeline author, I want pipeline-wide defaults for `image`, `dependencies`, and `variables`, so that I do not repeat configuration across jobs.
5. As a pipeline author, I want to override defaults per job, so that one job can use a different base image or dependency set.
6. As a pipeline author, I want every job to require `runs-on`, so that execution location is always explicit.
7. As a pipeline author, I want `only` glob filters on branch/tag names, so that jobs run only on relevant refs.
8. As a pipeline author, I want parallel jobs within a stage, so that independent work runs concurrently.
9. As a pipeline author, I want sequential stages, so that test runs after build completes.
10. As a pipeline author, I want different `runs-on` values within the same stage, so that I can compare platform vs org hardware in one push.
11. As a pipeline author, I want predefined immutable `CI_*` variables, so that scripts have stable metadata.
12. As a pipeline author, I want custom `variables:` for non-reserved names, so that I can parameterize scripts.
13. As a pipeline author, I want `GIT_DEPTH` to control clone depth, so that I trade history for speed.
14. As a pipeline author, I want ordered `dependencies:` with `installscript`, so that toolchains install predictably.
15. As a pipeline author, I want dependency `version` labels for humans only, so that I can track intent without cache coupling.
16. As a pipeline author, I want promoted layers used automatically when available, so that repeat jobs start faster.
17. As a pipeline author, I want logs to show layer hit vs live install, so that I can optimize recipes.
18. As a pipeline author, I want to opt into Docker via a dependency recipe, so that default sandboxes stay small.
19. As a pipeline author, I want `script` to run as `ogb` by default, so that user code is unprivileged.
20. As a pipeline author, I want optional `user: root` for scripts that need it, so that I am not blocked on edge cases.

### Pipeline execution and results

21. As a pipeline author, I want a push to `main` to trigger a **Pipeline Run**, so that CI runs without manual action.
22. As a pipeline author, I want no run created when CI file is absent, so that repos without CI are not noisy.
23. As a pipeline author, I want skipped jobs (non-matching `only`) omitted from the run, so that the UI reflects what actually ran.
24. As a pipeline author, I want a failed job to fail the stage and skip later stages, so that I do not waste compute on doomed pipelines.
25. As a pipeline author, I want in-flight jobs in a failing stage to finish, so that I get complete logs for diagnosis.
26. As a pipeline author, I want job logs streamed during execution, so that I can watch long builds.
27. As a pipeline author, I want structured log sections (layer mount, install, workspace, script), so that I can find failures quickly.
28. As a pipeline author, I want automatic job timeout, so that runaway scripts cannot run forever.
29. As a pipeline author with push access, I want to cancel a running job, so that I can stop mistaken builds.
30. As a pipeline author, I want commit pages to link to the run for that SHA, so that I see CI status in context.
31. As a pipeline author, I want a pipelines tab on the repository, so that I can browse run history.

### Visibility and access

32. As a visitor to a public repository, I want to read pipeline logs without signing in, so that open-source CI is transparent.
33. As a member of a private repository, I want pipeline logs restricted to members, so that CI output matches code confidentiality.
34. As a visitor on the pipelines tab when CI is not configured, I want an empty state with guidance, so that I know how to add `.opengitbase-ci.yml`.

### Organization compute

35. As an org owner, I want to enroll **Compute Nodes** without platform admin approval, so that I control my own hardware onboarding.
36. As an org owner, I want to set `HostingScope` to `OwnOrgOnly`, so that only my org's repos use my nodes for `organization-self-hosted`.
37. As an org owner, I want to set `HostingScope` to `CrossOrgAllowed`, so that my nodes serve `community-hosted` jobs.
38. As an org owner, I want to require `MaxConcurrentJobs`, `MaxCpu`, and `MaxMemoryBytes` at enrollment, so that I declare honest capacity.
39. As an org owner, I want to update capacity later, so that I can reflect hardware changes.
40. As an org owner, I want capacity reductions rejected while jobs exceed new limits, so that running work is not orphaned.
41. As an org owner, I want my agents to claim work via HTTPS long-poll only, so that I do not open Kafka connectivity from my network.
42. As an org member, I want to use `organization-self-hosted` in pipelines without enrolling nodes, so that infra changes stay with the owner.
43. As an org owner, I want to approve **Domain Allowance Requests** for my org, so that self-hosted jobs reach org-approved domains.

### Platform compute and administration

44. As a platform admin, I want to enroll **Platform Compute Nodes**, so that `ogb-hosted` capacity exists.
45. As a platform admin, I want platform agents to use Kafka job notifications, so that dispatch latency is low on platform hardware.
46. As a platform admin, I want to manage the **Base Image Catalog**, so that authors pick from approved slugs only.
47. As a platform admin, I want base images built from pinned OCI sources, so that supply chain is traceable.
48. As a platform admin, I want dependency usage analytics (count, duration, success rate), so that I know what to promote.
49. As a platform admin, I want promotion blocked unless the last five installs succeeded, so that I never promote broken layers.
50. As a platform admin, I want **Layer Promotion** builds only on platform nodes, so that layer supply chain stays trusted.
51. As a platform admin, I want layers stored in S3-compatible **Layer Store** with node caching, so that all nodes benefit.
52. As a platform admin, I want to approve platform **Domain Allowance Requests**, so that shared egress stays governed.
53. As a platform admin, I want conservative `ogb-hosted` defaults (1 vCPU, 2 GiB, 30 min), so that shared abuse surface is limited.

### Security, networking, and identity

54. As a security engineer, I want Firecracker per job, so that isolation does not rely on container namespaces alone.
55. As a security engineer, I want egress enforced on the host, so that guests cannot disable firewall rules.
56. As a security engineer, I want restricted default egress with allowlists, so that mining and exfiltration are harder.
57. As a security engineer, I want per-job credentials for repo access, so that node compromise does not leak all repositories.
58. As a security engineer, I want job credentials scoped to one SHA, so that blast radius is one commit.
59. As a security engineer, I want job credentials to expire at teardown, so that leaked tokens are short-lived.
60. As a security engineer, I want node credentials unable to read arbitrary repos, so that enrollment tokens are not sufficient for code access.
61. As a pipeline author, I want to request a new egress domain with justification, so that I can reach required package registries.
62. As a pipeline author, I want clear log messages when egress is denied, so that I know to request allowance.

### Operations and observability

63. As an operator, I want storage hooks to only report push metadata, so that git nodes stay simple.
64. As an operator, I want Kafka as the event backbone, so that new consumers can subscribe without hook changes.
65. As an operator, I want idempotent pipeline creation per commit, so that duplicate events do not duplicate runs.
66. As an operator, I want Postgres as job state of record, so that I can inspect and recover queue state.
67. As a support engineer, I want job status transitions logged (queued, running, passed, failed, cancelled), so that incidents are traceable.
68. As a support engineer, I want dependency install outcomes recorded, so that promotion decisions are evidence-based.

### Future integration (deferred v1)

69. As a merge request author, I want MR-triggered pipelines, so that CI gates integrate with **Approved** merge request state.
70. As a platform owner, I want secrets injection in pipelines, so that deploy keys and tokens are handled safely.
71. As a pipeline author, I want to publish build artifacts, so that downstream steps consume outputs.

---

## Implementation Decisions

### Deep modules (control plane)

The implementation should favor **deep modules** — substantial functionality behind narrow, stable interfaces that can be contract-tested in isolation.

#### 1. Git Push Ingestion

**Responsibility:** Accept push notifications from storage; validate caller; publish **Git Push Received**.

**Interface:**

```
IngestGitPush(repositoryId, ref, afterSha) → void
```

**Notes:** No YAML parsing. Idempotent publish acceptable; scheduler deduplicates.

#### 2. Event Bus Publisher

**Responsibility:** Serialize domain events to Kafka (3-broker cluster, RF=3).

**Topics (v1):**

| Topic | Payload intent |
|-------|----------------|
| `git.push.received` | Schedule pipeline evaluation |
| `ci.job.available` | Wake platform agents; org agents ignore |

#### 3. Pipeline Definition Parser

**Responsibility:** Parse and validate `.opengitbase-ci.yml` v1; resolve **Pipeline Defaults**; extract jobs, stages, hosting profiles, branch filters.

**Interface:**

```
ParsePipelineDefinition(yamlText) → PipelineDefinition | ValidationError[]
ResolveJob(jobName, definition, defaults) → ResolvedJob
```

**Notes:** Glob matcher for `only:` is a separate pure function. JSON Schema in web app mirrors parser rules.

#### 4. Pipeline Scheduler

**Responsibility:** Consume `git.push.received`; fetch CI file at commit; create **Pipeline Run**; orchestrate **Stages**; enqueue **Jobs**.

**Interface:**

```
HandleGitPushReceived(event) → void
AdvancePipelineRun(runId) → void   // after stage completion
```

**Rules:**

- Missing CI file → return without DB run row.
- Duplicate `repositoryId + afterSha` → no second run.
- Stage failure → do not schedule subsequent stages; let in-flight jobs complete.

#### 5. Job Queue and Lifecycle

**Responsibility:** Authoritative job state in Postgres; transitions `queued → running → passed|failed|cancelled`.

**Interface:**

```
EnqueueJob(resolvedJob, runId, stageName) → jobId
ClaimJob(nodeId, hostingProfiles[]) → ClaimResult | null
UpdateJobStatus(jobId, status, metadata) → void
CancelJob(jobId, requestedByUserId) → void
```

#### 6. Compute Node Registry

**Responsibility:** Enrollment, heartbeat, **Compute Node Capacity**, hosting scope, eligibility for hosting profiles.

**Interface:**

```
CreateEnrollment(operator, capacity, hostingScope) → enrollmentToken
RegisterNode(token, nodeMetadata) → nodeId + nodeIdentity
Heartbeat(nodeId, utilization) → void
UpdateCapacity(nodeId, capacity) → Result  // reject if below running count
ListEligibleNodes(hostingProfile, repoOrgId) → Node[]
```

**Notes:** Mirrors storage-node patterns. Org enrollment is org-Owner self-service.

#### 7. Job Identity Service

**Responsibility:** Mint and revoke short-lived **Job Identity** scoped to one job execution.

**Interface:**

```
MintJobIdentity(jobId, scopes[]) → credential
RevokeJobIdentity(jobId) → void
ValidateJobIdentity(credential, action, resource) → bool
```

**Scopes (v1):** repo-read at SHA, log-write, layer-store-read for job recipe, job-status-update.

#### 8. Job Dispatch Coordinator

**Responsibility:** Match queued jobs to nodes; write queue row; publish `ci.job.available`.

**Interface:**

```
DispatchNextJobs() → void   // called after enqueue or on timer
```

**Notes:** Org agents claim via long-poll; platform agents also subscribe to Kafka.

#### 9. Workspace Materialization Service (API-side or agent-side contract)

**Responsibility:** Fetch repository tree at SHA using **Job Identity**; produce worktree or shallow archive per `GIT_DEPTH`.

**Interface:**

```
MaterializeWorkspace(jobIdentity, gitDepth) → WorkspaceArtifact
```

**Notes:** Credentials never injected into guest environment.

#### 10. Base Image Catalog

**Responsibility:** Admin CRUD for allowed `image:` slugs; map slug → Layer Store hash + OCI provenance metadata.

**Interface:**

```
ResolveBaseImage(slug) → RootfsArtifactRef | NotFound
ListCatalogEntries() → CatalogEntry[]
```

#### 11. Dependency Layer Resolver

**Responsibility:** Compute layer key `sha256(baseSlug + normalizedInstallscript)`; resolve hit/miss; record **Dependency Install Outcome**.

**Interface:**

```
ResolveLayers(baseSlug, recipes[]) → LayerPlan  // mount order + live installs needed
RecordInstallOutcome(recipeKey, exitCode, durationMs) → void
GetPromotionStats(recipeKey) → PromotionStats
```

#### 12. Layer Promotion Service

**Responsibility:** Admin-initiated promotion when last 5 outcomes succeeded; schedule promotion job on platform node; upload delta to Layer Store.

**Interface:**

```
RequestPromotion(recipeKey) → promotionJobId | Ineligible
CompletePromotion(promotionJobId, layerHash) → void
```

#### 13. Layer Store Client

**Responsibility:** S3-compatible put/get for content-addressed blobs; used by promotion, agents, and catalog publishing.

**Interface:**

```
PutBlob(hash, stream) → void
GetBlob(hash) → stream
```

#### 14. Domain Allowance Request Workflow

**Responsibility:** User submissions with justification; route to platform admin or org admin; merge into **Platform Egress Allowlist** or **Org Egress Allowlist**.

**Interface:**

```
SubmitRequest(domain, justification, target: Platform|Org) → requestId
ApproveRequest(requestId, approver) → void
DenyRequest(requestId, approver, reason) → void
EffectiveAllowlist(hostingProfile, orgId) → DomainSet
```

#### 15. Pipeline Run API and Authorization

**Responsibility:** CRUD/read for runs, jobs, logs; enforce **Pipeline Run Visibility** and **Job Cancellation** rules.

**Interface:**

```
ListRuns(repoId, viewer) → RunSummary[]
GetRunLogs(jobId, viewer) → LogStream
CancelJob(jobId, viewer) → void   // requires repo write access
```

### Deep modules (compute agent)

#### 16. Sandbox Orchestrator (compute agent)

**Responsibility:** OverlayFS stack assembly, Firecracker boot/teardown, dependency installscript execution (root), script execution (**Job Execution User**), resource limits, timeout watchdog.

**Interface:**

```
ExecuteJob(jobPacket, jobIdentity) → JobResult
```

**Sub-responsibilities (internal):** layer cache, egress enforcement hook, log forwarding, workspace mount.

#### 17. Compute Agent Runtime

**Responsibility:** Enrollment, heartbeat, claim loop (Kafka wake + long-poll), Node Identity maintenance, local layer cache.

**Interface:**

```
RunAgent(config) → void   // long-running
```

**Notes:** Separate binary from storage agent. Requires KVM + Firecracker.

### Data model (conceptual)

| Entity | Purpose |
|--------|---------|
| ComputeNode | Registered executor host |
| ComputeNodeEnrollment | Token-based registration |
| PipelineRun | One YAML evaluation per commit |
| JobExecution | One schedulable unit with status |
| DependencyInstallOutcome | Per-recipe install telemetry |
| DependencyLayer | Promoted layer metadata + hash |
| BaseImageCatalogEntry | Allowed `image:` slug |
| DomainAllowanceRequest | Egress approval workflow |
| EgressAllowlistEntry | Platform or org domain row |

### CI variables (v1 predefined)

| Variable | Meaning |
|----------|---------|
| `CI_COMMIT_SHA` | Built commit |
| `CI_COMMIT_REF_NAME` | Branch or tag name |
| `CI_PROJECT_DIR` | Workspace path in guest |
| `CI_PROJECT_PATH` | `owner/repo` |
| `CI_PROJECT_PATH_SLUG` | Slugified path |
| `CI_JOB_NAME` | Job key from YAML |
| `CI_PIPELINE_ID` | Pipeline run ID |
| `CI_JOB_ID` | Job execution ID |
| `CI_RUNS_ON` | Hosting profile value |

### Resource defaults

| Profile | CPU | RAM | Timeout | Ephemeral disk |
|---------|-----|-----|---------|----------------|
| `ogb-hosted` | 1 | 2 GiB | 30 min | 20 GiB |
| org/community | up to node `MaxCpu` / `MaxMemoryBytes` | same | same cap | same cap |

### Web application surfaces

- **Pipelines UI** at repository pipelines route: list, detail, logs, cancel, **CI Not Configured State**.
- Commit page: status badge + link to run.
- User documentation section and JSON Schema for editors (already published).
- Admin: base image catalog, dependency promotion dashboard, domain requests, platform compute fleet.
- Org settings: compute enrollment, capacity, org egress approvals.

### Assumptions recorded

- Implementation delivers the **full vision** in one program of work; delivery slicing is a later planning exercise by the team.
- Kafka 3-broker cluster is provisioned in docker-compose alongside existing services.
- MinIO (or equivalent) is provisioned for Layer Store in compose.
- Merge request pipeline triggers integrate later via merge gate registry without renaming MR states.

---

## Testing Decisions

### What makes a good test

- Test **externally visible behavior and policy outcomes**, not internal mount commands or private orchestration sequencing.
- Prefer **contract tests** at deep module boundaries (parser, scheduler, layer resolver, identity service, egress composer).
- Integration tests prove push → Kafka → run → claim → sandbox → logs with compose infrastructure healthy (Kafka RF=3, MinIO reachable).

### Modules to test (priority)

| Module | Contract focus |
|--------|----------------|
| Pipeline Definition Parser | Defaults merge, stage order, `runs-on` required, glob `only`, `user` enum |
| Pipeline Scheduler | Idempotent run creation, silent no-op without CI file, stage gating on failure |
| Job Queue | Claim concurrency, cancel while running, status transitions |
| Compute Node Registry | Eligibility by hosting profile and scope; capacity update rejection |
| Job Identity Service | Scope enforcement; node identity cannot read repo |
| Dependency Layer Resolver | Key hashing; promotion stats; 5-streak eligibility |
| Layer Promotion Service | Ineligible when streak broken; eligible after recovery |
| Egress composer | Platform vs org vs community-effective allowlists |
| Domain Allowance workflow | Approve/deny routing |
| Workspace Materialization | `GIT_DEPTH` 0 vs N behavior |
| Sandbox Orchestrator | Layer mount vs installscript path; root vs ogb execution (agent integration) |

### Prior art in repository

- CQRS query/handler patterns in existing feature slices (storage node enrollment, repository replication).
- E2E compose stack with multi-node storage fleet and heartbeat gates.
- Playwright UI tests for repository and admin surfaces.
- MSW handlers for frontend API mocking — extend for pipeline run APIs.

### End-to-end scenarios

1. Push with valid CI file on matching branch → run created → job passes on `ogb-hosted`.
2. Push without CI file → no run row; pipelines tab shows not-configured state.
3. Dependency recipe live install → outcome recorded; promotion UI reflects stats.
4. Promotion after five successes → subsequent job mounts layer (log evidences cache hit).
5. Egress deny → log cites domain; request workflow adds domain → retry succeeds.
6. Org node long-poll claim → job runs with org allowlist composition.
7. Cancel with write access → job `cancelled`, VM torn down.
8. Public repo logs visible anonymously; private repo denied to non-member.

---

## Out of Scope (v1)

- Secrets management and masked variables in pipelines
- Build artifact publishing and download
- Merge request pipeline triggers and MR check UI
- Arbitrary user-uploaded base VM images or registry URLs in `image:`
- Per-job `resources:` YAML overrides
- Org-wide default variables in admin UI
- Per-job `when:` policies beyond stage-level skip-on-failure
- Unrestricted global egress
- Org-admin **Layer Promotion**
- Separate package-manager cache tier distinct from promoted layers
- Implementation delivery slicing (team plans slices separately)

---

## Further Notes

- **Firecracker** is the isolation substrate; scheduling, Kafka, OverlayFS layering, egress, identity, and UI are platform capabilities around it.
- `image:` selects a catalog **Base Image** for OverlayFS bottom layer — not container-per-job isolation.
- OverlayFS does not resolve semantic dependency conflicts; incompatible layers or installscripts must fail loudly in preparation or install phases.
- **Layer Promotion** captures deltas from **base only**; recipes that depend on prior recipes in the same list should remain unpromoted until chained promotion exists.
- Abuse resistance combines egress allowlists, `ogb-hosted` resource defaults, timeouts, and monitoring — not hypervisor isolation alone.
- The earlier `firecracker-ci-microvm-prd.md` draft is superseded by this document for product direction.

### Related artifacts

| Artifact | Location |
|----------|----------|
| Domain glossary | [CONTEXT.md](../../CONTEXT.md) |
| Push trigger ADR | [0001-pipeline-trigger-event-bus.md](../adr/0001-pipeline-trigger-event-bus.md) |
| User documentation | Web app `/docs/ci/*` |
| Pipeline JSON Schema | Web app `/schemas/opengitbase-ci.v1.json` |
