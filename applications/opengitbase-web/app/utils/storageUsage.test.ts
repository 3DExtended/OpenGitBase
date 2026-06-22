import { describe, expect, it } from 'vitest'
import {
  DEFAULT_STORAGE_BYTES_LIMIT,
  formatStorageBytes,
  formatUsagePercent,
  storageUsageState,
  usagePercent,
} from './storageUsage'

describe('usagePercent', () => {
  it('returns 0 when limit is zero', () => {
    expect(usagePercent(1_000, 0)).toBe(0)
  })

  it('returns 0 when limit is negative', () => {
    expect(usagePercent(1_000, -1)).toBe(0)
  })

  it('returns 0 for empty repository', () => {
    expect(usagePercent(0, DEFAULT_STORAGE_BYTES_LIMIT)).toBe(0)
  })

  it('keeps fractional percent for small usage (2.6 MB / 1 GB bug)', () => {
    const bytesUsed = 2.6 * 1_048_576
    const percent = usagePercent(bytesUsed, DEFAULT_STORAGE_BYTES_LIMIT)
    expect(percent).toBeGreaterThan(0)
    expect(percent).toBeLessThan(1)
    expect(Math.round(percent)).toBe(0)
  })

  it('returns ~49% for half-gigabyte usage', () => {
    expect(usagePercent(524_288_000, DEFAULT_STORAGE_BYTES_LIMIT)).toBeCloseTo(48.8, 1)
  })

  it('returns ~84% for warning-threshold usage', () => {
    expect(usagePercent(900_000_000, DEFAULT_STORAGE_BYTES_LIMIT)).toBeCloseTo(83.8, 0)
  })

  it('returns 100% at limit', () => {
    expect(usagePercent(DEFAULT_STORAGE_BYTES_LIMIT, DEFAULT_STORAGE_BYTES_LIMIT)).toBe(100)
  })

  it('caps percent at 100 when usage exceeds limit', () => {
    expect(usagePercent(DEFAULT_STORAGE_BYTES_LIMIT * 2, DEFAULT_STORAGE_BYTES_LIMIT)).toBe(100)
  })
})

describe('formatUsagePercent', () => {
  it('returns 0 for zero bytes used', () => {
    expect(formatUsagePercent(0, 0)).toBe('0')
    expect(formatUsagePercent(5, 0)).toBe('0')
  })

  it('returns one decimal for sub-1% usage', () => {
    expect(formatUsagePercent(0.25390625, 2_726_298)).toBe('0.3')
    expect(formatUsagePercent(0.9, 1_000)).toBe('0.9')
  })

  it('returns rounded integer for 1% and above', () => {
    expect(formatUsagePercent(1.4, 1_000)).toBe('1')
    expect(formatUsagePercent(48.8, 524_288_000)).toBe('49')
    expect(formatUsagePercent(83.9, 900_000_000)).toBe('84')
    expect(formatUsagePercent(100, DEFAULT_STORAGE_BYTES_LIMIT)).toBe('100')
  })
})

describe('formatStorageBytes', () => {
  it('formats kilobytes', () => {
    expect(formatStorageBytes(26_397)).toBe('26 KB')
    expect(formatStorageBytes(1023)).toBe('1 KB')
  })

  it('formats megabytes with one decimal', () => {
    expect(formatStorageBytes(2.6 * 1_048_576)).toBe('2.6 MB')
    expect(formatStorageBytes(524_288_000)).toBe('500.0 MB')
  })

  it('formats gigabytes with two decimals', () => {
    expect(formatStorageBytes(DEFAULT_STORAGE_BYTES_LIMIT)).toBe('1.00 GB')
    expect(formatStorageBytes(2.5 * 1_073_741_824)).toBe('2.50 GB')
  })
})

describe('storageUsageState', () => {
  it('reports normal state for small usage', () => {
    const bytesUsed = 2.6 * 1_048_576
    const state = storageUsageState(bytesUsed, DEFAULT_STORAGE_BYTES_LIMIT)

    expect(state.percent).toBeGreaterThan(0)
    expect(state.label).toBe('0.3')
    expect(state.warning).toBe(false)
    expect(state.atLimit).toBe(false)
  })

  it('reports warning state from 80% upward', () => {
    const state = storageUsageState(900_000_000, DEFAULT_STORAGE_BYTES_LIMIT)
    expect(state.warning).toBe(true)
    expect(state.atLimit).toBe(false)
    expect(state.label).toBe('84')
  })

  it('reports at-limit state from 100% upward', () => {
    const state = storageUsageState(DEFAULT_STORAGE_BYTES_LIMIT, DEFAULT_STORAGE_BYTES_LIMIT)
    expect(state.warning).toBe(true)
    expect(state.atLimit).toBe(true)
    expect(state.label).toBe('100')
  })

  it('matches the previously broken rounded-to-zero display', () => {
    const bytesUsed = 2_726_298
    const state = storageUsageState(bytesUsed, DEFAULT_STORAGE_BYTES_LIMIT)

    expect(Math.round(state.percent)).toBe(0)
    expect(state.percent).toBeGreaterThan(0)
    expect(state.label).not.toBe('0')
  })
})
