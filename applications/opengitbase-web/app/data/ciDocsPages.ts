export type CiDocPage = {
  slug: string
  title: string
  description: string
  markdown: string
}

export const CI_DOCS_SECTION_TITLE = 'CI/CD'

export const ciDocsPages: CiDocPage[] = [
  {
    slug: 'overview',
    title: 'Overview',
    description: 'What OpenGitBase CI/CD is and how the pieces fit together.',
    markdown: `# OpenGitBase CI/CD

OpenGitBase CI/CD runs your pipelines inside **Firecracker MicroVMs** on registered **Compute Nodes**. You define pipelines in a file at the repository root:

\`\`\`
.opengitbase-ci.yml
\`\`\`

## What you get

- **Isolated job sandboxes** — each job runs in its own MicroVM with an OverlayFS stack (base image + dependency layers + ephemeral writable layer).
- **Hybrid compute** — use platform capacity (\`ogb-hosted\`), your organization's hardware (\`organization-self-hosted\`), or federated community nodes (\`community-hosted\`).
- **Push-triggered runs (v1)** — a git push creates a **Pipeline Run**; merge request pipelines come later.
- **Editor support** — a published JSON Schema enables validation and autocompletion in VS Code and other YAML editors.

## Out of scope in v1

- Secrets management
- Publishing build artifacts
- Merge request pipeline triggers

## Documentation map

Start with [Quick start](/docs/ci/quick-start), then read [How it works](/docs/ci/how-it-works) for the end-to-end flow.`,
  },
  {
    slug: 'quick-start',
    title: 'Quick start',
    description: 'Add your first pipeline in a few minutes.',
    markdown: `# Quick start

## 1. Add \`.opengitbase-ci.yml\`

Commit this file to your repository root:

\`\`\`yaml
image: alpine:26.1.1

variables:
  GIT_DEPTH: 0

build:
  stage: build
  runs-on: ogb-hosted
  only:
    - main
  script: |
    echo "Building $CI_PROJECT_PATH at $CI_COMMIT_SHA"
\`\`\`

Every job **must** declare \`runs-on\`. There is no implicit default.

## 2. Push to \`main\`

When the commit lands on the primary storage node, OpenGitBase creates a **Pipeline Run** and schedules the \`build\` job (because \`only\` includes \`main\`).

## 3. Watch the run

Pipeline runs and logs appear in the web UI (implementation in progress). Failed jobs fail their **Stage**; later stages are skipped.

## Next steps

- [Pipeline YAML reference](/docs/ci/pipeline-yaml)
- [Hosting profiles](/docs/ci/hosting-profiles)
- [Editor setup](/docs/ci/editor-setup) for VS Code autocompletion`,
  },
  {
    slug: 'how-it-works',
    title: 'How it works',
    description: 'End-to-end flow from git push to job teardown.',
    markdown: `# How it works

## 1. Push

You \`git push\` to OpenGitBase. The primary storage node's \`post-receive\` hook validates the push and completes replication as today.

## 2. Event bus

The hook calls a thin internal API endpoint with \`repositoryId\`, \`ref\`, and \`afterSha\`. The API publishes **Git Push Received** to **Kafka** (topic \`git.push.received\`).

Storage nodes do **not** parse \`.opengitbase-ci.yml\`.

## 3. Pipeline scheduler

A scheduler consumer (consumer group \`pipeline-scheduler\`) handles **Git Push Received**:

1. Load \`.opengitbase-ci.yml\` at \`afterSha\` (no file → no-op).
2. Create a **Pipeline Run**.
3. Evaluate each job's \`only\` filter against the pushed branch.
4. Order **Stages** (from \`stages:\` or first-seen order).
5. Enqueue eligible jobs.

## 4. Job preparation (on a Compute Node)

The **Compute Node Agent** on the selected node:

1. Resolves \`image:\` to a **Base Image** catalog rootfs.
2. Mounts promoted **Dependency Layers** (or runs \`installscript\` blocks inside the live VM, in order).
3. Boots a Firecracker MicroVM with the composed OverlayFS stack.
4. **Workspace materialization** on the host:
   - \`GIT_DEPTH: 0\` → full git worktree bind-mounted at \`$CI_PROJECT_DIR\`
   - \`GIT_DEPTH: N\` → shallow tree archive unpacked into the guest
5. Injects **CI variables** and runs \`script\` as the **Job Execution User** (default \`ogb\`; override per job with \`user:\`).
6. Streams logs using a per-job **Job Identity** (not the node enrollment token).
7. Tears down the MicroVM.

## 5. Stages and failure

- Jobs in the same **Stage** run in parallel.
- **Stages** run one after another.
- If any job in a stage fails, that stage fails; in-flight siblings still finish; later stages are skipped.

## Identity model

| Credential | Lifetime | Purpose |
|------------|----------|---------|
| **Node Identity** | Long-lived | Enrollment, heartbeat, claim work |
| **Job Identity** | Per job | Read repo at SHA, logs, fetch base/layer blobs, report status |

- **Pipeline Run Visibility** follows repository visibility: public → world-readable logs; private → members only.

## Job dispatch

1. Scheduler writes the job to a Postgres queue and publishes \`ci.job.available\` to Kafka.
2. **Platform agents** consume Kafka for fast wake; **org agents** long-poll the claim API over HTTPS (no Kafka client required on org networks).
3. Agent claims the job → receives job spec + **Job Identity**. Agents never accept inbound connections.`,
  },
  {
    slug: 'pipeline-yaml',
    title: 'Pipeline YAML',
    description: 'Structure of `.opengitbase-ci.yml` v1.',
    markdown: `# Pipeline YAML reference

File location: **repository root** → \`.opengitbase-ci.yml\`

## Top-level keys

| Key | Required | Description |
|-----|----------|-------------|
| \`stages\` | No | Ordered list of stage names. If omitted, stage order follows first job appearance in the file. |
| \`image\` | Yes* | **Pipeline default** base image catalog slug. |
| \`dependencies\` | No | Ordered **Pipeline default** dependency recipes. |
| \`variables\` | No | **Pipeline default** variables (see [Variables](/docs/ci/variables)). |
| \`<job-name>\` | Yes (≥1) | Job definition (one or more). |

*Each job may override \`image\`.

## Job keys

| Key | Required | Description |
|-----|----------|-------------|
| \`stage\` | Yes | Stage name for grouping and ordering. |
| \`runs-on\` | Yes | [Hosting profile](/docs/ci/hosting-profiles): \`ogb-hosted\`, \`organization-self-hosted\`, or \`community-hosted\`. |
| \`only\` | No | Glob patterns matched against the pushed ref name (e.g. \`main\`, \`release/*\`). Job is skipped when no pattern matches. |
| \`script\` | Yes | Shell script after setup (default user \`ogb\`) |
| \`user\` | No | \`ogb\` (default) or \`root\` for \`script\` only |
| \`image\` | No | Override pipeline default base image. |
| \`dependencies\` | No | Override pipeline default dependencies. |
| \`variables\` | No | Merge with pipeline default variables (non-reserved names only). |

## \`only:\` branch filter

\`only\` is a list of **glob patterns** compared to \`CI_COMMIT_REF_NAME\` (the branch or tag name from the push):

| Pattern | Matches |
|---------|---------|
| \`main\` | Exactly \`main\` |
| \`release/*\` | \`release/1.0\`, \`release/2026-q1\`, etc. |
| \`*-stable\` | Any ref ending in \`-stable\` |

If \`only\` is omitted, the job is eligible on every push. If present and nothing matches, the job is skipped for that **Pipeline Run**.

Glob semantics follow simple \`*\` wildcard rules (v1: no \`**\` recursion).

## Full example

\`\`\`yaml
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
  script: |
    dotnet build

test:
  stage: test
  runs-on: ogb-hosted
  only:
    - main
  script: |
    dotnet test
\`\`\`

## \`image:\` is not \`docker pull\`

\`image:\` selects a slug from the [Base Image Catalog](/docs/ci/base-images). The platform builds and pins a rootfs from a known OCI source. Compute Nodes cache the artifact for OverlayFS.`,
  },
  {
    slug: 'hosting-profiles',
    title: 'Hosting profiles',
    description: 'Choose where jobs run with `runs-on`.',
    markdown: `# Hosting profiles

Every job **must** set \`runs-on\`. There is no default.

## \`ogb-hosted\`

Runs on **Platform Compute Nodes** operated by OpenGitBase.

- Best for getting started without owning hardware.
- Conservative default **Job Resource Limits**: 1 vCPU, 2 GiB RAM, 30 min timeout, 20 GiB ephemeral disk.
- Egress uses the [Platform Egress Allowlist](/docs/ci/network-egress) only.

## \`organization-self-hosted\`

Runs on **Org Compute Nodes** registered by your organization with \`HostingScope: OwnOrgOnly\`.

- Only repositories owned by that organization are eligible.
- Org **Owners** enroll nodes (self-service — no platform admin token required).
- Egress: platform defaults **∪** [Org Egress Allowlist](/docs/ci/network-egress).
- Nodes advertise CPU/memory capacity; scheduler respects node limits.

## \`community-hosted\`

Runs on **Org Compute Nodes** that opted into \`HostingScope: CrossOrgAllowed\`.

- Federated shared capacity contributed by organizations.
- Any repository may schedule here (subject to node capacity).
- Egress: platform allowlist only (same as \`ogb-hosted\`).

## Choosing a profile

| Need | Profile |
|------|---------|
| Zero setup | \`ogb-hosted\` |
| Compliance / on-prem for your org | \`organization-self-hosted\` |
| Volunteer or shared community compute | \`community-hosted\` |`,
  },
  {
    slug: 'compute-nodes',
    title: 'Compute nodes',
    description: 'Register hosts that execute CI jobs.',
    markdown: `# Compute nodes

A **Compute Node** is a registered host running the **opengitbase-compute-agent**. The agent receives jobs, composes OverlayFS stacks, boots Firecracker MicroVMs, and sends heartbeats.

## Platform nodes

Platform administrators enroll **Platform Compute Nodes** with an enrollment token. These nodes execute \`ogb-hosted\` jobs and **Layer Promotion** builds.

## Organization nodes

Organization **Owners** enroll nodes **without platform admin involvement**:

1. Open organization settings → Compute.
2. Create an enrollment token (required: \`HostingScope\`, \`MaxConcurrentJobs\`, \`MaxCpu\`, \`MaxMemoryBytes\`).
3. Install the compute agent on your host and register with the token.

### Hosting scope

| Scope | \`runs-on\` profile |
|-------|-------------------|
| \`OwnOrgOnly\` | \`organization-self-hosted\` |
| \`CrossOrgAllowed\` | \`community-hosted\` |

## Requirements

- Linux host with KVM and Firecracker support
- Network connectivity to the OpenGitBase API and Layer Store
- Sufficient disk for local base-image and dependency-layer caches

## Separate from storage nodes

Compute nodes are a distinct fleet from [storage nodes](/docs/ci/overview). A host may run both agents in the future, but enrollment and identity are separate in v1.`,
  },
  {
    slug: 'base-images',
    title: 'Base images',
    description: 'Curated root filesystems for MicroVM boot.',
    markdown: `# Base images

The \`image:\` field in \`.opengitbase-ci.yml\` selects a **Base Image** — the bottom OverlayFS layer for the job sandbox.

## Catalog slugs

Values like \`alpine:26.1.1\` are **catalog slugs**, not raw registry URLs. Each slug maps to:

- A pinned rootfs artifact (squashfs/ext4 image)
- A known OCI source used at build time (e.g. \`docker.io/library/alpine:3.26.1\`)
- A content hash for cache verification

## Admin-managed catalog

Platform administrators add new allowed slugs to the **Base Image Catalog**. Users cannot reference arbitrary images in v1.

## Caching

Compute Nodes cache resolved rootfs artifacts locally. Jobs do not pull from Docker Hub at runtime.

## Overrides

Set \`image:\` on a specific job to override the pipeline default for that job only.`,
  },
  {
    slug: 'dependencies',
    title: 'Dependencies & layers',
    description: 'Declare toolchains and how layer promotion speeds up jobs.',
    markdown: `# Dependencies and layers

The \`dependencies:\` list is **ordered**. Each entry is a **Dependency Recipe**:

\`\`\`yaml
dependencies:
  - dotnet-sdk:
    - version: 11
    - installscript: |
        sudo apk add dotnet10-sdk
\`\`\`

- \`version\` is metadata for humans and admin analytics — **not** the cache lookup key.
- The cache key is \`sha256(base_image_slug + normalized_installscript)\`.

## At job time

For each recipe in order:

1. If a promoted **Dependency Layer** exists for this base image + installscript → mount as OverlayFS layer before VM start.
2. Otherwise → run \`installscript\` inside the live MicroVM after start.

## Layer promotion (platform admin)

Platform administrators review dependency usage in an admin UI that shows, **per recipe**:

| Metric | Purpose |
|--------|---------|
| Usage count | How often the recipe appears in pipelines |
| Avg install duration | Cost of unpromoted installs |
| Success rate | Share of installs where \`installscript\` exited 0 |
| Failed install count | Visible warning — **not promotion-eligible** |

Only recipes whose **last 5 installs all succeeded** can be promoted (v1). Older failures stay visible in stats but do not block promotion once the streak recovers. Failed installs are never promoted.

Promotion builds run only on **Platform Compute Nodes**.
- Each layer is a filesystem **delta from the base image alone** (not from a composed stack).
- Artifacts are stored in the central **Layer Store** (S3-compatible; MinIO in compose) and cached on every compute node.

Org nodes never build promoted layers, but they mount them.

## Self-contained recipes

Promoted layers are built from the base image only. Recipes that assume a prior dependency already ran should remain unpromoted until chained promotion exists.

## Container tooling (Docker)

Docker (CLI and/or engine) is **not** included in default base images. To run \`docker build\` in \`script\`, declare an explicit dependency:

\`\`\`yaml
dependencies:
  - docker:
    - version: 1
    - installscript: |
        sudo apk add docker docker-cli
        sudo rc-service docker start
\`\`\`

You may also need a [domain allowance request](/docs/ci/network-egress) for \`registry-1.docker.io\`.`,
  },
  {
    slug: 'variables',
    title: 'Variables',
    description: 'Predefined CI_* variables and your `variables:` block.',
    markdown: `# Variables

## Pipeline and job \`variables:\`

Top-level \`variables:\` are **Pipeline Defaults**. Jobs may add or override **non-reserved** names.

Reserved \`CI_*\` names are injected by the platform and **cannot** be overridden.

## \`GIT_DEPTH\`

Special variable controlling [workspace materialization](/docs/ci/how-it-works):

| Value | Behavior |
|-------|----------|
| \`0\` | Full git worktree mounted at \`$CI_PROJECT_DIR\` |
| \`N > 0\` | Shallow tree archive (depth N) unpacked into the guest |

Also exported to the job environment.

## Predefined variables (v1)

| Variable | Description |
|----------|-------------|
| \`CI_COMMIT_SHA\` | Commit being built |
| \`CI_COMMIT_REF_NAME\` | Branch name |
| \`CI_PROJECT_DIR\` | Workspace path inside the VM (typically \`/workspace\`) |
| \`CI_PROJECT_PATH\` | \`owner/repo\` |
| \`CI_PROJECT_PATH_SLUG\` | Slugified project path |
| \`CI_JOB_NAME\` | Job key from YAML |
| \`CI_PIPELINE_ID\` | Pipeline run ID |
| \`CI_JOB_ID\` | Job execution ID |
| \`CI_RUNS_ON\` | Hosting profile value |`,
  },
  {
    slug: 'network-egress',
    title: 'Network egress',
    description: 'Allowlists and domain allowance requests.',
    markdown: `# Network egress

Outbound traffic from job MicroVMs is **host-enforced** (outside the guest). v1 uses restricted allowlists.

## Default allowlist (platform)

\`ogb-hosted\` and \`community-hosted\` jobs use the **Platform Egress Allowlist**, including defaults such as:

- Alpine package mirrors
- \`registry-1.docker.io\` / \`auth.docker.io\`

## Organization allowlist

\`organization-self-hosted\` jobs use:

\`\`\`
effective = platform_defaults ∪ org_egress_allowlist
\`\`\`

## Requesting a new domain

Submit a **Domain Allowance Request** with:

- Domain name (e.g. \`nuget.org\`)
- Detailed justification (why the pipeline needs it)

### Approval paths

| Request target | Approver | Applies to |
|----------------|----------|------------|
| Platform | Platform admin | Platform egress allowlist |
| Organization | Org admin | Org egress allowlist |

On deny or missing allowance, job logs show an egress denial with guidance to request access.

## Security note

Unrestricted egress on shared infrastructure is not the v1 default. Long runtimes plus open internet are treated as an abuse risk.`,
  },
  {
    slug: 'editor-setup',
    title: 'Editor setup',
    description: 'Validate `.opengitbase-ci.yml` in VS Code.',
    markdown: `# Editor setup

OpenGitBase publishes a JSON Schema for \`.opengitbase-ci.yml\` v1.

## VS Code

1. Install the [YAML extension](https://marketplace.visualstudio.com/items?itemName=redhat.vscode-yaml) by Red Hat.
2. Add to your workspace \`.vscode/settings.json\`:

\`\`\`json
{
  "yaml.schemas": {
    "https://your-instance.example/schemas/opengitbase-ci.v1.json": ".opengitbase-ci.yml"
  }
}
\`\`\`

Replace the URL with your instance origin (same host as the web UI). The schema is served from \`/schemas/opengitbase-ci.v1.json\`.

## What you get

- Autocompletion for \`runs-on\`, \`stage\`, and structure keys
- Validation errors for missing required fields (e.g. \`runs-on\` on every job)
- Enum hints for hosting profiles

## Local development

When running the web app locally, use:

\`\`\`
http://localhost:3000/schemas/opengitbase-ci.v1.json
\`\`\`

(or your configured dev port)`,
  },
]

export function getCiDocPage(slug: string): CiDocPage | undefined {
  return ciDocsPages.find((page) => page.slug === slug)
}

export function getCiDocSlugs(): string[] {
  return ciDocsPages.map((page) => page.slug)
}
