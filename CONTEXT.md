# OpenGitBase — CI/CD

CI/CD runs user-defined pipelines from `.opengitbase-ci.yml` on isolated execution environments scheduled by the control plane.

## Language

**Compute Node**:
A registered host that executes CI jobs on behalf of the platform or an organization.
_Avoid_: Runner host, worker, agent machine

**Platform Compute Node**:
A Compute Node operated by the platform (`OwnerOrganizationId` is null).
_Avoid_: Shared runner, cloud runner

**Org Compute Node**:
A Compute Node contributed and operated by an organization (`OwnerOrganizationId` set).
_Avoid_: Self-hosted runner, custom runner

**Hosting Profile**:
A required per-Job `runs-on` value selecting the **Compute Node** pool: `ogb-hosted` (platform nodes), `organization-self-hosted` (owning org's `OwnOrgOnly` nodes), or `community-hosted` (any org's `CrossOrgAllowed` nodes).
_Avoid_: Runner label, runner tag, pool

**Pipeline Schema**:
A machine-readable JSON Schema for `.opengitbase-ci.yml` published by the platform, enabling editor validation and autocompletion (e.g. VS Code YAML extension).
_Avoid_: Pipeline DSL spec

**Pipeline Run**:
A single evaluation of `.opengitbase-ci.yml` for one repository commit, triggered by a git push in v1. If the file is absent at the pushed commit, the scheduler performs a silent no-op (no run row).
_Avoid_: Workflow run, build

**CI Not Configured State**:
The empty-state shown on the repository **Pipelines UI** when no `.opengitbase-ci.yml` exists on the default branch (or no runs have occurred yet), guiding authors to add a pipeline file.
_Avoid_: CI disabled, pipeline off

**Pipeline Run Visibility**:
Who may view a **Pipeline Run** and its logs: public repositories are world-readable; private repositories require repository membership (same bar as code browsing).
_Avoid_: Build visibility, log ACL

**Job Cancellation**:
Manual teardown of a running **Job** requested by a user with repository write access (same bar as triggering a push pipeline); the control plane signals the agent to destroy the **Job Sandbox** and marks the job `cancelled`.
_Avoid_: Abort, stop build

**Job Execution User**:
The Unix user that runs a job's `script` phase inside the **Job Sandbox**. Default: unprivileged `ogb`. Optional per-job `user:` in YAML may be `ogb` or `root` only (v1). `installscript` phases always run as root.
_Avoid_: Run as, sudo user

**Pipelines UI**:
Repository-scoped web surfaces for **Pipeline Runs**: list at `/{owner}/{repo}/pipelines`, run detail pages, and commit-page linkage showing status for the run at that SHA.
_Avoid_: CI dashboard, builds page

**Pipeline**:
The set of jobs defined in a repository's `.opengitbase-ci.yml` for a given commit or merge-request event.
_Avoid_: Workflow, build definition

**Job**:
A single schedulable unit within a Pipeline, defined as a top-level key in `.opengitbase-ci.yml` (e.g. `build`, `test`).
_Avoid_: Step, task

**Pipeline Defaults**:
Top-level `image`, `dependencies`, and `variables` in `.opengitbase-ci.yml` that apply to every **Job** unless overridden at the job level.
_Avoid_: Global config, template

**Stage**:
A named phase grouping one or more **Jobs**. Jobs in the same **Stage** run in parallel; **Stages** run sequentially. Order comes from an explicit top-level `stages:` array when present, otherwise from first-seen job order in the file. If any **Job** in a **Stage** fails, that **Stage** is failed and later **Stages** are skipped (in-flight jobs in the current **Stage** still finish).
_Avoid_: Phase, step group

**Base Image**:
A platform-curated root filesystem used as the bottom OverlayFS layer when booting a Firecracker MicroVM for a Job; referenced by catalog slug in `image:` (e.g. `alpine:26.1.1`).
_Avoid_: Container image (in isolation context), VM template, docker pull target

**Base Image Catalog**:
The admin-managed list of allowed `image:` slug values, each mapping to a pinned rootfs artifact built from a known OCI source.
_Avoid_: Image registry, docker hub

**Dependency Layer**:
A reusable OverlayFS layer produced from a dependency's `installscript`, promoted by a platform admin when usage warrants it; keyed by `sha256(base_image_slug + normalized_installscript)`. Each layer is a filesystem delta captured from the **Base Image** alone (not from a composed stack).
_Avoid_: Cache blob, toolchain image

**Dependency Install Outcome**:
The recorded success or failure of a **Dependency Recipe**'s `installscript` for a specific job, including exit code and duration.
_Avoid_: Setup result, install status

**Layer Promotion**:
The admin-only workflow that builds a **Dependency Layer** by running one `installscript` on a fresh **Base Image** VM and snapshotting the result. Promotion builds run exclusively on **Platform Compute Nodes**; finished artifacts are stored in central durable storage and cached locally on any **Compute Node** that mounts them. A recipe is promotion-eligible when its last N **Dependency Install Outcomes** are all successful (v1: N=5); older failures remain visible in stats but do not block promotion once the streak recovers.
_Avoid_: Cache warmup, layer upload

**Layer Store**:
The platform's central durable object storage (S3-compatible API; MinIO in docker-compose, S3-compatible endpoint in production) for **Base Image** rootfs artifacts and promoted **Dependency Layer** blobs, keyed by content hash, with a local cache on each **Compute Node**.
_Avoid_: Artifact registry, blob storage

**Dependency Recipe**:
A declared dependency entry in `.opengitbase-ci.yml` (name, version, ordered `installscript`) regardless of whether a promoted layer exists yet. Name and version are author metadata and admin analytics labels, not layer lookup keys.
_Avoid_: Package, toolchain block

**Job Sandbox**:
The Firecracker MicroVM plus its composed OverlayFS stack (base image + dependency layers + ephemeral writable upper layer) for one Job execution.
_Avoid_: Container, runner environment

**Job Identity**:
A short-lived credential minted by the control plane for one **Job** execution, distinct from the **Compute Node**'s enrollment identity. Scoped to: read the triggering repo at `CI_COMMIT_SHA`, stream logs for this job run, fetch **Base Image** and **Dependency Layer** artifacts for this job's recipe, and update this job's lifecycle status. Expires at teardown.
_Avoid_: Runner token, node token, PAT

**Compute Node Agent**:
The long-lived process on a registered host that receives job assignments, composes OverlayFS stacks, boots Firecracker MicroVMs, and reports heartbeats. Separate from the storage node agent.
_Avoid_: Runner, worker process

**Compute Node Enrollment**:
Registration of a **Compute Node** into the fleet. Platform **Compute Nodes** are enrolled by a platform admin via enrollment token; org **Compute Nodes** are self-service — the org **Owner** creates an enrollment token without platform admin involvement.
_Avoid_: Runner registration, node signup

**Node Identity**:
The long-lived credential tied to a registered **Compute Node**, used for enrollment, heartbeat, receiving work assignments, and reporting capacity — not for repo or cross-job access.
_Avoid_: Agent token, machine credential

**CI Variable**:
An environment variable injected into the **Job Sandbox** before `script` runs. Platform predefined `CI_*` variables are immutable; the repo-level `variables:` block in `.opengitbase-ci.yml` may add or override non-reserved names. `GIT_DEPTH` is special — it controls **Workspace Materialization** depth and is also exported to the guest.
_Avoid_: Env var, pipeline param

**Branch Filter**:
A job-level `only` list of glob patterns matched against `CI_COMMIT_REF_NAME` when a **Pipeline Run** is created (e.g. `main`, `release/*`).
_Avoid_: Branch whitelist, only-if

**Container Tooling**:
Optional guest capability (e.g. Docker CLI/engine) installed only when declared as a **Dependency Recipe** — never part of the default **Base Image**.
_Avoid_: Docker-in-Docker, DinD

**Workspace Materialization**:
How repository source is placed at `$CI_PROJECT_DIR` before `script` runs. v1 uses host-agent delivery: full git worktree mount when `GIT_DEPTH: 0`, shallow tree archive when `GIT_DEPTH > 0`.
_Avoid_: Checkout, clone step

**Egress Policy**:
Host-enforced outbound network rules for a **Job Sandbox** MicroVM. v1 uses restricted allowlists per **Hosting Profile**; enforcement happens outside the guest. Effective allowlist composition: `ogb-hosted` and `community-hosted` use the **Platform Egress Allowlist** only; `organization-self-hosted` uses platform defaults ∪ **Org Egress Allowlist**.
_Avoid_: Firewall rules, network mode

**Domain Allowance Request**:
A user-submitted request to add a destination domain to an egress allowlist, with required justification. Routed either to a platform admin (platform-wide list) or to the repository's org admin (org-scoped list).
_Avoid_: Network exception, firewall ticket

**Platform Egress Allowlist**:
Domains approved by a platform admin through **Domain Allowance Request**; enforced on jobs using platform-operated capacity.
_Avoid_: Global firewall list

**Org Egress Allowlist**:
Domains approved by an org admin through **Domain Allowance Request**; enforced on that org's jobs when using org-operated capacity.
_Avoid_: Org firewall list

**Job Resource Limit**:
CPU, memory, timeout, and ephemeral disk ceilings applied to a **Job Sandbox**. v1 `ogb-hosted` platform defaults: 1 vCPU, 2 GiB RAM, 30 minute timeout, 20 GiB ephemeral disk. Org/community nodes may size sandboxes up to advertised capacity.
_Avoid_: Quota, resource request

**Compute Node Capacity**:
The required enrollment declaration for a **Compute Node**: `MaxConcurrentJobs`, `MaxCpu`, and `MaxMemoryBytes`. Set by the enrolling operator; updatable later by platform admin or org Owner, but not below current running job count.
_Avoid_: Node quota, hardware spec

**Job Dispatch**:
Delivery of a scheduled **Job** to a **Compute Node Agent**: the control plane writes the job to a database queue (source of truth), publishes a Kafka availability event, and the agent **claims** the job via API to receive the job spec and minted **Job Identity**. **Platform Compute Node Agents** subscribe to Kafka for low-latency wake; **Org Compute Node Agents** use HTTPS long-poll claim only (no Kafka client).
_Avoid_: Runner assignment, work steal

**Git Push Received**:
The domain event emitted when a commit lands on a repository primary storage node after a successful push; published to Kafka topic `git.push.received`.
_Avoid_: Webhook, hook callback

**Event Bus**:
The platform's internal publish/subscribe channel implemented as a **3-broker Apache Kafka cluster** (KRaft) in v1, including local docker-compose, decoupling git-side effects from downstream consumers like the pipeline scheduler.
_Avoid_: Message queue, webhook dispatcher

## Relationships

- A **Compute Node** belongs to either the platform or exactly one **Organization** (via `OwnerOrganizationId`)
- Top-level `image`, `dependencies`, and `variables` are **Pipeline Defaults** inherited by every **Job** unless a job overrides them
- Jobs within the same **Stage** run in parallel (each may use a different **Hosting Profile**); **Stages** execute sequentially
- A **Pipeline** contains one or more **Jobs** grouped into **Stages**
- A **Pipeline Run** is created on git push (v1) when `.opengitbase-ci.yml` exists at the pushed commit (`Git Push Received` via Kafka); missing file → silent no-op
- The **Pipelines UI** shows a **CI Not Configured State** when no pipeline file is present (empty state with setup guidance)
- **Pipeline Run Visibility** follows repository visibility: public → world-readable logs; private → members only
- **Job Cancellation** requires repository write access; automatic timeout cancellation still applies via **Job Resource Limits**
- **Job Execution User** defaults to `ogb` for `script`; `installscript` always runs as root; per-job `user:` overrides the script user
- **Pipelines UI** exposes runs at `/{owner}/{repo}/pipelines` with commit-page status linkage
- Each **Job** must declare a **Hosting Profile** via required `runs-on`; there is no implicit default pool
- Each **Job** evaluates its `only` **Branch Filter** glob patterns against the pushed ref name before scheduling
- The scheduler matches a **Job**'s **Hosting Profile** to eligible **Compute Nodes** (respecting org ownership and hosting scope), enqueues it in the database, and publishes a Kafka job-availability event
- A **Compute Node Agent** claims queued jobs via API and receives a **Job Identity** with the job packet — agents do not accept inbound dispatch connections; org agents long-poll, platform agents may also consume Kafka `ci.job.available`
- A **Job** runs on exactly one **Compute Node** selected by the scheduler inside a **Job Sandbox**
- A **Job Sandbox** is composed from one **Base Image**, zero or more **Dependency Layers** (in declared order), and an ephemeral writable upper layer
- A **Base Image** slug must exist in the **Base Image Catalog** before a Job can use it
- A **Dependency Recipe** resolves to a promoted **Dependency Layer** (mounted at boot) or runs its `installscript` inside the live MicroVM after start
- **Container Tooling** (e.g. Docker) is available only when declared as a **Dependency Recipe**, not in default **Base Images**
- A **Dependency Layer** lookup key is `sha256(base_image_slug + normalized_installscript)` — the same script on different **Base Image** slugs produces different layers
- **Layer Promotion** requires the last 5 **Dependency Install Outcomes** for a recipe to be successful; failed installs appear in admin stats but older failures do not block promotion after a recovery streak
- Promoted **Dependency Layer** and **Base Image** artifacts live in the S3-compatible **Layer Store** (MinIO in compose); every **Compute Node** fetches and caches by content hash before mounting
- A **Job** receives a unique **Job Identity** at schedule time with repo-read, log-write, asset-fetch, and status-update scopes; the **Node Identity** alone cannot access repository content
- Repository source is **Workspace Materialization** on the host agent, then mounted into the **Job Sandbox** at `$CI_PROJECT_DIR` — never cloned inside the guest with platform credentials
- Guest egress is governed by an **Egress Policy** enforced on the host, not inside the MicroVM
- Additional destinations require a **Domain Allowance Request** with justification — to a platform admin (**Platform Egress Allowlist**) or org admin (**Org Egress Allowlist**)
- **Egress Policy** effective allowlist: `ogb-hosted` and `community-hosted` → platform list only; `organization-self-hosted` → platform defaults ∪ org list
- **Job Resource Limits** on `ogb-hosted`: 1 vCPU, 2 GiB RAM, 30 min timeout, 20 GiB ephemeral disk; org/community nodes bounded by advertised capacity
- **Org Compute Nodes** mirror the enrollment and ownership model of **Storage Nodes** (heartbeat, `HostingScope`, capacity), but org enrollment is self-service — no platform admin token required
- **Compute Node Capacity** is required at enrollment and may be updated later, but not below the node's current running job count

## Example dialogue

> **Dev:** "Can org A's pipeline run on org B's Compute Node?"
> **Domain expert:** "Only if org B's node has `HostingScope: CrossOrgAllowed` and org A's pipeline targets a label that node advertises."

> **Dev:** "Do we need orgs to bring their own compute?"
> **Domain expert:** "No — authors can set `runs-on: ogb-hosted` to use platform **Compute Nodes**. Org hardware is opt-in via `runs-on: organization-self-hosted`."

> **Dev:** "Can I omit `runs-on`?"
> **Domain expert:** "No — every **Job** must declare a **Hosting Profile**. The **Pipeline Schema** enforces it in the editor."

> **Dev:** "Is `image: alpine:26.1.1` a Docker pull?"
> **Domain expert:** "No — it selects a **Base Image** catalog slug. The platform builds and pins the rootfs from a known OCI source; Compute Nodes cache the artifact. Admins add new allowed slugs to the **Base Image Catalog**."

> **Dev:** "When does `installscript` run?"
> **Domain expert:** "Only when no promoted **Dependency Layer** exists for that recipe. Otherwise the layer is mounted into the OverlayFS stack before the VM starts."

> **Dev:** "What if the author changes `installscript` but keeps `version: 11`?"
> **Domain expert:** "It's a cache miss — lookup is by **Base Image** + `installscript` hash, not version. Version is for humans and admin usage stats."

> **Dev:** "Can an org admin promote a dependency layer on their own hardware?"
> **Domain expert:** "No — **Layer Promotion** is platform-admin only and runs on **Platform Compute Nodes** so the supply chain stays trusted."

> **Dev:** "What's `community-hosted`?"
> **Domain expert:** "Jobs run on **Org Compute Nodes** that opted into `HostingScope: CrossOrgAllowed` — federated shared capacity, not platform hardware."

> **Dev:** "Does the compute node token fetch every repo?"
> **Domain expert:** "No — each **Job** gets its own **Job Identity** scoped to that run. The node identity is only for enrollment, heartbeats, and receiving work assignments."

> **Dev:** "Can my org allow `nuget.org` without waiting for platform?"
> **Domain expert:** "Yes — submit a **Domain Allowance Request** to your org admin. On `organization-self-hosted` jobs, the effective allowlist is platform defaults plus the **Org Egress Allowlist**."

> **Dev:** "Can I promote a dependency that often fails install?"
> **Domain expert:** "Only after the last 5 installs all succeeded. The admin UI still shows historical failure rate and duration — you never promote from a broken streak."

> **Dev:** "How does the agent get work?"
> **Domain expert:** "**Job Dispatch** is hybrid: job rows live in the database, Kafka notifies availability, the agent claims via API and receives a **Job Identity**. No inbound connections to org nodes."

> **Dev:** "Who parses `.opengitbase-ci.yml`?"
> **Domain expert:** "The API control plane, after the pipeline scheduler consumes **Git Push Received** from the **Event Bus**. Storage hooks stay thin — they only report repo, ref, and commit SHA."

## Flagged ambiguities

- "Runner" appears in E2E test tooling and Git industry jargon — in CI/CD context use **Compute Node** for the registered host and reserve "agent" for the long-lived process on that host.
