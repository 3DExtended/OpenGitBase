export const ORG_STORAGE_BOOTSTRAP_SCRIPT_URL =
  'https://raw.githubusercontent.com/3DExtended/OpenGitBase/main/scripts/bootstrap-org-storage-node.sh'

export interface OrgStorageBootstrapParams {
  enrollmentToken: string
  nodeId: string
  apiUrl: string
  internalHost: string
}

export function buildOrgStorageBootstrapInvocation(params: OrgStorageBootstrapParams): string {
  const escapedToken = params.enrollmentToken.replace(/"/g, '\\"')
  const escapedNodeId = params.nodeId.replace(/"/g, '\\"')
  const escapedApiUrl = params.apiUrl.replace(/"/g, '\\"')
  const escapedHost = params.internalHost.replace(/"/g, '\\"')

  return [
    `curl -fsSL "${ORG_STORAGE_BOOTSTRAP_SCRIPT_URL}" | bash -s -- \\`,
    `  --token "${escapedToken}" \\`,
    `  --node-id "${escapedNodeId}" \\`,
    `  --api-url "${escapedApiUrl}" \\`,
    `  --internal-host "${escapedHost}"`,
  ].join('\n')
}

export function buildOrgStorageBootstrapDownloadScript(params: OrgStorageBootstrapParams): string {
  return `#!/usr/bin/env bash
set -euo pipefail
curl -fsSL "${ORG_STORAGE_BOOTSTRAP_SCRIPT_URL}" | bash -s -- \\
  --token "${params.enrollmentToken}" \\
  --node-id "${params.nodeId}" \\
  --api-url "${params.apiUrl}" \\
  --internal-host "${params.internalHost}"
`
}

export function gibiToBytes(gibi: number): number {
  return Math.max(0, Math.round(gibi * 1024 ** 3))
}

export function bytesToGibi(bytes: number): number {
  return Math.max(1, Math.round(bytes / (1024 ** 3)))
}
