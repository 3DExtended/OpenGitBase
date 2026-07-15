export type PublicHealthStatus = 0 | 1 | 2

export interface PublicStatusIncident {
  message: string
  severity: 0 | 1 | 2
  updatedAt: string
}

export interface StatusInstanceSnapshot {
  instanceId: string
  status: PublicHealthStatus
  lastCheckedAt: string
  responseTimeMs?: number | null
  lastSeenAt?: string | null
  message?: string | null
}

export interface StatusGroupSnapshot {
  group: number
  status: PublicHealthStatus
  instances: StatusInstanceSnapshot[]
}

export interface PublicStatusSnapshot {
  overallStatus: PublicHealthStatus
  checkedAt: string
  groups: StatusGroupSnapshot[]
  incident: PublicStatusIncident | null
}

export interface PublicStatusHistoryDay {
  date: string
  uptimePercent: number
  healthyRatio: number
  degradedRatio: number
  unhealthyRatio: number
}

export interface PublicStatusHistoryGroupSeries {
  group: number
  days: PublicStatusHistoryDay[]
}

export interface PublicStatusHistory {
  groups: PublicStatusHistoryGroupSeries[]
  overall: PublicStatusHistoryDay[]
  overallStateMix: PublicStatusHistoryDay[]
}

export function healthStatusLabel(status: PublicHealthStatus): 'healthy' | 'degraded' | 'unhealthy' {
  if (status === 0) return 'healthy'
  if (status === 1) return 'degraded'
  return 'unhealthy'
}

export function componentGroupKey(group: number): string {
  switch (group) {
    case 1: return 'website'
    case 2: return 'api'
    case 3: return 'git'
    case 4: return 'storage'
    case 5: return 'dataStores'
    case 6: return 'messageBus'
    default: return 'unknown'
  }
}

export function incidentSeverityKey(severity: 0 | 1 | 2): 'info' | 'warning' | 'outage' {
  if (severity === 2) return 'outage'
  if (severity === 1) return 'warning'
  return 'info'
}
