export const DEFAULT_STORAGE_BYTES_LIMIT = 1_073_741_824

export function usagePercent(bytesUsed: number, bytesLimit: number): number {
  if (bytesLimit <= 0) {
    return 0
  }
  return Math.min(100, (bytesUsed / bytesLimit) * 100)
}

export function formatUsagePercent(percent: number, bytesUsed: number): string {
  if (bytesUsed <= 0 || percent <= 0) {
    return '0'
  }
  if (percent < 1) {
    return percent.toFixed(1)
  }
  return String(Math.round(percent))
}

export function formatStorageBytes(bytes: number): string {
  if (bytes >= 1_073_741_824) {
    return `${(bytes / 1_073_741_824).toFixed(2)} GB`
  }
  if (bytes >= 1_048_576) {
    return `${(bytes / 1_048_576).toFixed(1)} MB`
  }
  return `${(bytes / 1024).toFixed(0)} KB`
}

export function storageUsageState(bytesUsed: number, bytesLimit: number) {
  const percent = usagePercent(bytesUsed, bytesLimit)
  return {
    percent,
    label: formatUsagePercent(percent, bytesUsed),
    warning: percent >= 80,
    atLimit: percent >= 100,
  }
}
