export interface StorageDocPage {
  slug: string
  title: string
  description: string
  markdown: string
}

export const STORAGE_DOCS_SECTION_TITLE = 'Storage'

export const storageDocsPages: StorageDocPage[] = [
  {
    slug: 'org-storage-nodes',
    title: 'Organization storage nodes',
    description: 'Register hosts that store Git repositories for your organization.',
    markdown: `# Organization storage nodes

A **storage node** runs the OpenGitBase **repo-storage-layer** agent. It stores bare Git repositories, serves internal git HTTP, participates in encrypted replication, and reports capacity heartbeats to the API.

This is separate from [compute nodes](/docs/ci/compute-nodes). A host may run both agents in the future, but enrollment and identity are separate.

## Self-service enrollment

Organization **owners** enroll nodes without platform admin approval:

1. Open **Organization settings → Storage** (\`/{org}/storage\`).
2. Create an enrollment with **Node ID**, **max capacity (GiB)**, and **hosting scope**.
3. Copy the bootstrap command or download the generated shell script.
4. Run the script on a Linux host with Docker, git, openssl, and curl.

## Bootstrap script

The canonical installer is \`scripts/bootstrap-org-storage-node.sh\` in the OpenGitBase repository.

\`\`\`bash
curl -fsSL https://raw.githubusercontent.com/3DExtended/OpenGitBase/main/scripts/bootstrap-org-storage-node.sh | bash -s -- \\
  --token "<enrollment-token>" \\
  --node-id "org-storage-1" \\
  --api-url "https://api.example.com/api" \\
  --internal-host "storage.example.com"
\`\`\`

The script shallow-clones OpenGitBase, generates node PKI material, fetches the fleet dispatcher SSH public key using your enrollment token, builds the storage agent image, and starts a container.

Optional port overrides: \`--ssh-port\`, \`--http-port\`, \`--git-http-port\`, \`--mtls-port\`.

## Networking and firewall

Platform **dispatchers** reach your node using the **internal host** you provide. It must resolve to an address reachable from the platform fleet.

Publish these ports (defaults):

| Port | Purpose |
|------|---------|
| 22 | Git over SSH from dispatchers |
| 8081 | Internal storage HTTP API |
| 8082 | Internal git smart HTTP |
| 8443 | Peer mTLS replication |

If you use NAT or a reverse proxy, set port overrides on the bootstrap script and ensure dispatchers can still reach the registered host and ports.

## Hosting scope

| Scope | Meaning |
|-------|---------|
| **Own org only** | Node stores repositories owned by your organization |
| **Cross-org allowed** | Node may host **encrypted replica** copies for other orgs (never plaintext cross-org) |

You can change hosting scope after registration from the org storage settings page.

## Quota credits

Each healthy org-owned node with declared **max capacity** contributes to your organization's **effective byte limit**:

\`\`\`
effective limit = platform limit + sum(healthy org node max bytes)
\`\`\`

Increasing a node's max capacity increases contributed quota once the node is healthy.

Decreasing max capacity below current **used bytes** is rejected. To shrink capacity later you must move repositories off the node first (future platform rebalance workflow).

## Self-host tiers

Placement policy on the org storage page controls how many copies of each repository live on org-owned nodes versus platform or community nodes. See the [Encrypted Replica Storage PRD](/docs/prd/encrypted-replica-storage) for tier layouts (0–3 org nodes).

## Troubleshooting

- **Registration fails:** verify enrollment token not expired (7-day default) and node ID matches.
- **Unhealthy node:** check \`docker logs\` on the agent container and API reachability.
- **Git push fails:** confirm dispatchers can reach \`internalHost\` on SSH and git HTTP ports.`,
  },
]

export function getStorageDocPage(slug: string): StorageDocPage | undefined {
  return storageDocsPages.find((page) => page.slug === slug)
}

export function getStorageDocSlugs(): string[] {
  return storageDocsPages.map((page) => page.slug)
}
