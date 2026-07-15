<!-- forge: #8 -->

# PRD: CI/CD Runtime Completion (Firecracker Execution Substrate)

**Status:** Design complete (grill-me session 2026-07-12).  
**Parent PRD:** [ci-cd-pipelines.md](./ci-cd-pipelines.md) — full product vision.  
**Supersedes nothing** — this PRD covers the **remaining runtime, security, and policy gaps** after the ci-01…ci-20 and ci-prd-01…15 tracer bullets.  
**ADRs:** [0001-pipeline-trigger-event-bus.md](../adr/0001-pipeline-trigger-event-bus.md), [0002-compute-firecracker.md](../adr/0002-compute-firecracker.md).  
**Domain glossary:** [CONTEXT.md](../../CONTEXT.md).

---

## Problem Statement

OpenGitBase has a working CI/CD **control plane tracer**: `.opengitbase-ci.yml` parsing, push-triggered **Pipeline Runs**, Postgres job queue, **Compute Node** enrollment, hybrid `runs-on` routing APIs, admin and org UIs, Kafka/MinIO compose infrastructure, and a compose E2E that turns pushes green on a **process sandbox** fallback.

What is **not** complete relative to the parent PRD is the **Job Sandbox execution substrate** and the **security contracts** around it:

- **Firecracker MicroVM** boot does not occur — even when KVM is present, execution falls through to host `sh -c`.
- **Job Identity** is minted at claim time but never used; workspace materialization clones host bare-repo paths instead of scoped API fetch.
- **OverlayFS** assembly exists but often degrades to directory copy; layers are not booted as a guest rootfs.
- **Egress policy** is a static script URL pre-check, not host-enforced network control.
- **Dependency Layer** keys, promotion artifacts, and install failure handling do not match PRD semantics.
- **Resource defaults** for `ogb-hosted` are stored in the database but not applied at runtime.
- **Job Cancellation**, log streaming, agent authentication, and several UI/API authorization gaps remain.

Teams cannot rely on this stack for production hybrid compute until the runtime path on bare-metal **Platform Compute Nodes** and **Org Compute Nodes** delivers real MicroVM isolation, scoped credentials, and enforced policy — while compose retains an explicit dev fallback per ADR 0002.

---

## Solution

Complete the CI/CD **runtime layer** on top of the existing control plane:

1. **Production isolation** — Boot one Firecracker MicroVM per **Job** with OverlayFS-composed guest rootfs; run `installscript` and `script` **inside the guest** via a vsock guest agent.
2. **Scoped workspace fetch** — New control-plane **Workspace Materialization** API returns a commit-scoped archive authenticated by **Job Identity**; the agent shares it into the guest via virtio-fs (or equivalent 9p share).
3. **Two-tier credentials** — **Node Identity** (long-lived, orchestration only) authenticates agent API calls; **Job Identity** (short-lived, one SHA) authorizes workspace and layer fetch only.
4. **Host egress enforcement** — nftables/ipset on the tap interface: default deny, allowlisted domains resolved via host DNS into timed ipsets; denial logs guide **Domain Allowance Requests**.
5. **Real layer system** — PRD recipe keys (`sha256(baseSlug + normalizedInstallscript)`), fail-fast on failed installs, dedicated **Layer Promotion** jobs on **Platform Compute Nodes** that capture overlay deltas.
6. **Operable base images** — Operator script builds curated rootfs tarballs from pinned OCI sources (including guest agent and `ogb` user); platform-pinned kernel per agent.
7. **Lifecycle completeness** — Firecracker resource limits from job spec; cancel via poll + `ci.job.cancelled` Kafka wake with VM teardown; incremental log posts with SSE UI tailing.
8. **Verification split** — Compose keeps `PreferProcessSandbox`; add optional compose Firecracker profile and a **mandatory KVM bare-metal E2E gate** before marking runtime slices done.

---

## User Stories

### Firecracker execution and Job Sandbox

1. As a security engineer, I want each **Job** to run in a dedicated Firecracker MicroVM, so that isolation does not rely on container namespaces alone.
2. As a pipeline author, I want `installscript` phases to run as root inside the guest, so that package installation can use system paths predictably.
3. As a pipeline author, I want `script` to run as the **Job Execution User** `ogb` by default, so that user code is unprivileged.
4. As a pipeline author, I want optional `user: root` in job YAML to run `script` as root inside the guest, so that edge-case tooling is not blocked.
5. As an operator, I want compose development to keep process sandbox fallback, so that macOS and non-KVM environments can iterate without blocking.
6. As an operator, I want a KVM-verified E2E gate on bare-metal, so that "compose green" is not mistaken for production isolation.
7. As an operator, I want an optional compose Firecracker profile with privileged KVM mount, so that Linux developers can validate MicroVM paths locally.
8. As a platform admin, I want conservative `ogb-hosted` CPU, RAM, disk, and timeout limits enforced by Firecracker config, so that shared abuse surface is bounded.
9. As an operator, I want the MicroVM destroyed on job completion, failure, cancel, and timeout, so that resources are not leaked.

### Workspace materialization and Job Identity

10. As a security engineer, I want workspace fetched via **Job Identity** scoped to one commit SHA, so that node compromise does not expose arbitrary repositories.
11. As a security engineer, I want job credentials to expire and revoke at teardown, so that leaked tokens have minimal blast radius.
12. As a security engineer, I want **Node Identity** unable to read repository contents, so that enrollment credentials are insufficient for code access.
13. As a pipeline author, I want `GIT_DEPTH: 0` to materialize a full worktree at `$CI_PROJECT_DIR`, so that history-dependent scripts work.
14. As a pipeline author, I want `GIT_DEPTH: N` to materialize a shallow archive, so that I can trade history for speed.
15. As a security engineer, I want credentials never injected into the guest environment, so that job scripts cannot exfiltrate long-lived tokens.

### Compute Node Agent authentication and dispatch

16. As an org owner, I want my **Compute Node Agent** to authenticate outbound API calls with **Node Identity**, so that only registered nodes can claim work and post status.
17. As a platform admin, I want platform agents to wake on `ci.job.available` Kafka events, so that dispatch latency stays low.
18. As an org owner, I want agents to claim work via HTTPS polling without Kafka, so that I do not open inbound connectivity from my network.
19. As an operator, I want accurate running-job counts in heartbeats, so that capacity and dispatch reflect reality.

### Egress policy

20. As a security engineer, I want egress enforced on the host at the tap interface, so that guests cannot disable firewall rules.
21. As a security engineer, I want default deny egress on `ogb-hosted`, so that mining and exfiltration are harder on shared infrastructure.
22. As a pipeline author, I want clear log messages when egress is denied at runtime, so that I know to submit a **Domain Allowance Request**.
23. As a platform admin, I want production to start with an empty platform egress allowlist, so that egress is explicitly approved.
24. As a developer, I want a compose seed script for minimal dev egress entries, so that local iteration is practical without widening production defaults.
25. As an org owner, I want to approve org-scoped **Domain Allowance Requests** from the org compute settings UI, so that self-hosted jobs reach org-approved domains.
26. As a pipeline author on an org-owned repository, I want org egress allowlists composed with platform defaults for `organization-self-hosted`, so that policy matches hosting profile.

### Dependency layers and promotion

27. As a platform admin, I want dependency recipe keys content-addressed by base slug and installscript, so that promotion and cache hits are correct across bases and script changes.
28. As a pipeline author, I want a failed `installscript` to fail the **Job** immediately, so that later `script` phases do not run on broken prerequisites.
29. As a pipeline author, I want logs to show layer cache hit versus live install, so that I can optimize recipes.
30. As a platform admin, I want **Layer Promotion** to run as a dedicated internal job on a **Platform Compute Node**, so that layer supply chain stays trusted.
31. As a platform admin, I want promoted layers stored as real OverlayFS deltas in the **Layer Store**, so that subsequent jobs mount reproducible artifacts.
32. As a platform admin, I want promotion blocked unless the last five **Dependency Install Outcomes** succeeded, so that broken layers are never published.

### Base Image Catalog supply chain

33. As a platform admin, I want base images built from pinned OCI sources via an operator script, so that rootfs provenance is traceable.
34. As an operator, I want base image rootfs artifacts to include the vsock guest agent and `ogb` user, so that MicroVMs can execute scripts without per-job injection.
35. As an operator, I want one platform-pinned guest kernel per compute agent, so that Firecracker boot configuration stays simple in v1.

### Job lifecycle, logs, and cancellation

36. As a pipeline author, I want job logs streamed during execution, so that I can watch long builds.
37. As a pipeline author, I want structured log sections preserved (layer, install, workspace, script), so that I can find failures quickly.
38. As a pipeline author with repository write access, I want to cancel a running job and have the MicroVM torn down, so that mistaken builds stop promptly.
39. As a security engineer, I want users without write access unable to cancel jobs, so that cancellation matches push authority.
40. As a support engineer, I want cancel signaled via Kafka to platform agents and poll during in-flight execution for all agents, so that cancellation is timely and reliable.

### CI variables and org context

41. As a pipeline author, I want predefined `CI_*` variables including `CI_PROJECT_PATH`, `CI_PROJECT_PATH_SLUG`, and `CI_JOB_ID`, so that scripts have stable metadata.
42. As an org owner, I want `CI_ORGANIZATION_ID` set when the repository owner is an organization, so that org egress policy resolves correctly.

### Operations and verification

43. As an operator, I want a bare-metal Firecracker E2E script required in the release checklist, so that runtime completion is objectively verified.
44. As an operator, I want process-sandbox compose E2E to remain for fast CI feedback, so that control-plane regressions are caught without KVM.
45. As a support engineer, I want agent and scheduler behavior to remain idempotent under duplicate events, so that incident recovery is safe.

### Regression protection (existing behavior preserved)

46. As a pipeline author, I want existing parser, scheduler, stage gating, and `only` filter behavior unchanged, so that authoring semantics remain stable.
47. As a visitor to a public repository, I want to read pipeline logs without signing in, so that open-source CI stays transparent.
48. As a member of a private repository, I want pipeline logs restricted to members, so that CI output matches code confidentiality.

---

## Implementation Decisions

### Completion bar (isolation)

- **Production path:** Real Firecracker MicroVM boot on bare-metal **Platform Compute Nodes** and **Org Compute Nodes** when KVM is available and `PreferProcessSandbox` is false.
- **Compose path:** Process sandbox remains the default (`PreferProcessSandbox: true` per ADR 0002).
- **Done criteria:** Passing the mandatory KVM bare-metal E2E gate — compose green alone does not satisfy runtime completion.

### Deep modules (control plane — new or extended)

#### 1. Workspace Materialization Service

**Responsibility:** Produce commit-scoped workspace archives for a **Job**, authenticated by **Job Identity**.

**Interface:**

```
MaterializeWorkspaceArchive(jobIdentity, gitDepth) → WorkspaceArchive
  // full worktree archive when gitDepth == 0
  // shallow archive when gitDepth > 0
ValidateJobIdentityForWorkspace(jobIdentity, repositoryId, afterSha) → bool
```

**Notes:** Credentials never enter the guest. Replaces host bare-repo clone path. Scheduler continues to avoid injecting secrets into `EnvironmentJson`.

#### 2. Job Identity Service (runtime integration)

**Responsibility:** Mint, validate, revoke **Job Identity**; enforce SHA and repository scope; separate from **Node Identity**.

**Scopes (v1):** repo-read at SHA (workspace archive only), log-write, layer-store-read for job recipe, job-status-update.

**Notes:** Validator must not scan all identity rows in production — index by job or use constant-time lookup strategy.

#### 3. Node Identity Service

**Responsibility:** Issue long-lived **Node Identity** at successful registration; validate on agent API calls.

**Interface:**

```
RegisterNode(enrollmentToken, nodeMetadata) → nodeId + nodeIdentity
ValidateNodeIdentity(nodeIdentity, action) → bool
```

**Notes:** Node Identity authorizes claim, heartbeat, status update, log append — not repository read.

#### 4. Agent API Authentication Middleware

**Responsibility:** Require valid **Node Identity** on agent-facing endpoints (claim, heartbeat, status, dependency outcomes). **Job Identity** required only on workspace archive endpoint.

#### 5. Workspace Archive HTTP API

**Responsibility:** Expose `GET` (or `POST`) archive download for agents presenting **Job Identity** bearer token.

#### 6. Cancel Authorization

**Responsibility:** Enforce repository **write access** on **Job Cancellation** before marking `cancelled` or publishing cancel events.

#### 7. Cancel Event Publisher

**Responsibility:** Publish `ci.job.cancelled` to Kafka when a running job is cancelled (platform agent wake).

**Topic (v1 extension):**

| Topic | Payload intent |
|-------|----------------|
| `ci.job.cancelled` | Wake platform agents; signal in-flight teardown |

#### 8. Log Append and SSE Fan-out

**Responsibility:** Accept incremental log line appends from agents; expose SSE tail endpoint per job for **Pipelines UI**.

**Interface:**

```
AppendJobLogs(jobId, nodeIdentity, section, lines[]) → void
TailJobLogs(jobId, viewer) → SSE stream
```

#### 9. CI Variable Composer (scheduler extension)

**Responsibility:** Emit full v1 predefined `CI_*` set; resolve `CI_ORGANIZATION_ID` when repository owner is an organization; pass **Job Execution User** to agent environment protocol (not as a secret).

#### 10. Dependency Recipe Key Resolver

**Responsibility:** Compute `sha256(normalize(baseSlug) + normalize(installscript))` consistently across scheduler analytics, agent telemetry, and promotion.

#### 11. Layer Promotion Job Scheduler

**Responsibility:** Enqueue internal promotion **Job** on **Platform Compute Node** after admin approval and 5-success eligibility; capture overlay upper delta from base-only install; upload to **Layer Store**.

**Notes:** Replaces inline text-blob promotion worker behavior.

#### 12. Platform Egress Bootstrap (compose only)

**Responsibility:** Documented seed script adds minimal dev allowlist entries in compose; production starts empty.

### Deep modules (Compute Node Agent — new or extended)

#### 13. Firecracker Launcher (production implementation)

**Responsibility:** Boot one MicroVM per **Job** with configured vCPU, memory, disk; attach tap; load platform-pinned kernel; use OverlayFS merged root as guest rootfs; destroy VM on all exit paths.

**Notes:** Replaces host `sh -c` delegation when KVM + Firecracker binary available.

#### 14. Vsock Guest Agent Protocol

**Responsibility:** Guest-side agent in **Base Image** rootfs; host sends execute requests `{user, cwd, script, env}`; streams stdout/stderr lines over vsock.

**Phases:** `installscript` as root; `script` as **Job Execution User**.

#### 15. Virtio-fs Workspace Mount

**Responsibility:** After host fetches workspace archive via **Job Identity**, extract to temp dir and share read-write (or read-only per policy) into guest at `$CI_PROJECT_DIR`.

#### 16. Host Egress Enforcer (runtime)

**Responsibility:** Per job: resolve effective allowlist (platform and org per hosting profile); resolve allowed domains to ipset; apply nftables rules on tap; default DROP; log denials with **Domain Allowance Request** guidance.

**Notes:** Replaces script-only URL parsing as the primary enforcement mechanism. Static preflight may remain as supplementary signal.

#### 17. OverlayFS Stack Assembler (hardening)

**Responsibility:** Prefer real overlay mount on Linux; copy fallback acceptable only in compose dev profile; mount promoted **Dependency Layers** in order; teardown includes umount and temp cleanup.

#### 18. In-flight Cancel Watcher

**Responsibility:** Poll job status during vsock execution; on `cancelled`, destroy MicroVM and exit; complement Kafka wake between jobs.

#### 19. Incremental Log Forwarder

**Responsibility:** Forward vsock stream lines to control plane as they arrive (not only post-phase batches).

#### 20. Resource Limit Applier

**Responsibility:** Map job DB fields (`CpuLimit`, `MemoryMiB`, `DiskGiB`, `TimeoutSeconds`) to Firecracker VM configuration and host watchdog.

### Deep modules (operator tooling)

#### 21. Base Image Build Script

**Responsibility:** Build curated Dockerfile/OCI image → export rootfs tarball → upload to **Layer Store** → create **Base Image Catalog** entry with `ociProvenance` and `contentHash`.

**Notes:** Includes vsock guest agent binary and `ogb` user. Kernel is not per-catalog-entry in v1 — pinned on agent host.

#### 22. Compose Firecracker Profile

**Responsibility:** Optional compose profile: privileged agent, `/dev/kvm` mount, `PreferProcessSandbox: false`, Firecracker binary in image.

#### 23. Bare-metal Firecracker E2E Gate

**Responsibility:** Script proving push → schedule → claim → real MicroVM → pass on KVM host; required for runtime completion sign-off.

### Web application surfaces

- **Org compute settings:** Add pending org **Domain Allowance Request** review (approve/deny) alongside enrollment and capacity.
- **Pipelines run detail:** Replace polling-only logs with SSE tail; retain section grouping.
- No change to parser JSON Schema beyond documenting full `CI_*` set if needed.

### Event bus extensions

| Topic | Purpose |
|-------|---------|
| `ci.job.cancelled` | Platform agent cancel wake |

Existing topics `git.push.received` and `ci.job.available` unchanged per ADR 0001.

### Data model changes (minimal)

- **Node Identity** storage: hashed token linked to **Compute Node** registration row (or equivalent).
- No change to repository ownership model: organization-owned repos continue using owner id in `OwnerUserId`; scheduler resolves org vs user for `CI_ORGANIZATION_ID`.
- **Dependency Install Outcome** and promotion entities remain; recipe key computation changes behavior, not necessarily schema.

### Implementation phasing

| Phase | Focus |
|-------|--------|
| **1 — Security contracts** | Node Identity, workspace archive API, Job Identity integration, cancel write ACL, `CI_ORGANIZATION_ID` |
| **2 — Firecracker core** | Base image script, guest agent, real launcher, virtio-fs workspace, vsock execution |
| **3 — Policy runtime** | nftables egress, FC resource limits, install fail-fast, PRD recipe keys |
| **4 — Ops surfaces** | Promotion jobs, SSE logs, Kafka cancel, org egress UI |
| **5 — Verification** | Compose Firecracker profile, bare-metal E2E gate |

### Assumptions

- Existing control plane (parser, scheduler, queue, enrollment, UIs, Kafka, MinIO) remains the foundation and is not rewritten.
- ci-01…ci-20 and ci-prd-01…15 tracer work is treated as **scaffolding** until Phase 5 gate passes.
- Guest kernel version is uniform per agent host in v1.
- Virtio-fs (or 9p) is acceptable for workspace sharing; block-device workspace is out of scope for v1.
- Chained **Dependency Recipe** promotion (recipe depending on prior recipe in same list) remains deferred per parent PRD notes.

---

## Testing Decisions

### What makes a good test

- Test **externally visible behavior and policy outcomes**: job passes/fails, cancel stops execution, egress deny/allow, promotion eligibility, identity rejection across repos/SHAs.
- Prefer **contract tests** at deep module boundaries (workspace archive auth, recipe key hashing, egress composer, node vs job identity separation).
- Do not assert internal mount command strings or Firecracker CLI argument order — test outcomes (guest file visibility, exit codes, denial logs).

### Modules to test (priority)

| Module | Contract focus |
|--------|----------------|
| Workspace Materialization Service | Job Identity scope; GIT_DEPTH 0 vs N archive shape; reject cross-repo/SHA |
| Node Identity Service | Agent endpoints reject missing/invalid token; token cannot fetch workspace |
| Job Identity Service | Cannot read other repos; revoked/expired rejected |
| Cancel Authorization | Write required; cancel triggers terminal status |
| Dependency Recipe Key Resolver | Same base+script → same key; whitespace normalization |
| Install fail-fast | Failed installscript → job Failed, script not run |
| Layer Promotion Job Scheduler | Ineligible below 5 streak; eligible schedules platform job |
| Host Egress Enforcer | Default deny; allowlisted domain resolves; denial log content |
| Firecracker Launcher | Integration on KVM host: job passes, VM gone after teardown |
| Log SSE | Incremental lines visible to authorized viewer |

### Verification layers

| Layer | When | Command / environment |
|-------|------|------------------------|
| Unit / handler tests | Always | `dotnet test` on touched projects |
| Compose E2E (process sandbox) | Control-plane regressions | `scripts/test-pipelines-e2e.sh` |
| Compose profile (Firecracker) | Optional dev | `docker compose --profile firecracker` |
| Bare-metal E2E gate | Runtime completion | `scripts/test-pipelines-firecracker-e2e.sh` on KVM host |
| Visual snapshots | Org egress UI | `pnpm test:visual` for compute page changes |

### Prior art

- CQRS query/handler patterns in pipeline and compute-node features.
- `JobIdentitySecurityContractTests` — extend for workspace and node identity separation.
- `OrgComputeIntegrationTests` — extend for authenticated agent flows.
- Playwright visual specs for `org-compute`, `pipelines`.
- ADR 0002 dev fallback policy for compose test expectations.

### End-to-end scenarios (runtime completion)

1. KVM host: push → `ogb-hosted` job → real MicroVM → script passes → VM destroyed.
2. Job Identity: node token cannot download workspace; job token cannot access other repository SHA.
3. Failed `installscript` → job Failed → stage gating skips later stages.
4. Promotion after five successes → subsequent job logs layer cache hit with real artifact.
5. Egress deny at runtime → denial log → **Domain Allowance Request** → approve → retry succeeds.
6. Cancel by write user → VM teardown within poll window; non-write user receives 403.
7. Org-owned repo `organization-self-hosted` → org egress allowlist applied.
8. Compose process-sandbox E2E still passes unchanged.

---

## Out of Scope

Per parent PRD v1 deferrals (unchanged):

- Merge request pipeline triggers and MR check UI
- Secrets management and masked variables
- Build artifact publishing and download
- Per-job `resources:` YAML overrides beyond platform defaults
- Org-admin **Layer Promotion** (platform admin only)
- Arbitrary user-uploaded base VM images
- Separate package-manager cache tier distinct from promoted layers
- Per-job `when:` policies beyond stage-level skip-on-failure
- mTLS for **Node Identity** (bearer token sufficient for v1)
- Per-catalog-entry guest kernels (single pinned kernel per agent in v1)
- Chained **Dependency Recipe** promotion across ordered dependency lists

---

## Further Notes

- This PRD **does not replace** [ci-cd-pipelines.md](./ci-cd-pipelines.md). It defines the second implementation program needed to make that vision real on the execution path.
- ADR 0002 remains authoritative for compose vs production posture.
- Planning artifacts (`planning/ci-cd-prd-completion/`) marked runtime items complete should be re-opened or superseded by work items derived from this PRD until the bare-metal gate passes.
- Abuse resistance requires egress allowlists, resource defaults, timeouts, **and** MicroVM isolation — not hypervisor isolation alone.
- Parent PRD user stories 69–71 (MR pipelines, secrets, artifacts) remain deferred.

### Related artifacts

| Artifact | Location |
|----------|----------|
| Parent PRD | [ci-cd-pipelines.md](./ci-cd-pipelines.md) |
| Domain glossary | [CONTEXT.md](../../CONTEXT.md) |
| Firecracker ADR | [0002-compute-firecracker.md](../adr/0002-compute-firecracker.md) |
| Event bus ADR | [0001-pipeline-trigger-event-bus.md](../adr/0001-pipeline-trigger-event-bus.md) |
| Prior completion planning | [planning/ci-cd-prd-completion/](../../planning/ci-cd-prd-completion/) |
