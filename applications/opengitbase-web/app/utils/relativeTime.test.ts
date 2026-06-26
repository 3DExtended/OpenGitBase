import { describe, expect, it } from 'vitest'
import { formatAbsoluteTime, formatRelativeTime } from './relativeTime'

const now = new Date('2026-06-26T12:00:00.000Z')

describe('formatRelativeTime', () => {
  it('formats seconds in the past', () => {
    expect(formatRelativeTime('2026-06-26T11:59:30.000Z', { now })).toBe('30 seconds ago')
  })

  it('formats minutes in the past', () => {
    expect(formatRelativeTime('2026-06-26T11:45:00.000Z', { now })).toBe('15 minutes ago')
  })

  it('formats hours in the past', () => {
    expect(formatRelativeTime('2026-06-26T08:00:00.000Z', { now })).toBe('4 hours ago')
  })

  it('formats days in the past', () => {
    expect(formatRelativeTime('2026-06-24T12:00:00.000Z', { now })).toBe('2 days ago')
  })

  it('formats months in the past', () => {
    expect(formatRelativeTime('2026-04-26T12:00:00.000Z', { now })).toBe('2 months ago')
  })

  it('formats years in the past', () => {
    expect(formatRelativeTime('2024-06-26T12:00:00.000Z', { now })).toBe('2 years ago')
  })

  it('formats future dates', () => {
    expect(formatRelativeTime('2026-07-01T12:00:00.000Z', { now })).toBe('in 5 days')
  })
})

describe('formatAbsoluteTime', () => {
  it('formats a readable absolute datetime', () => {
    const formatted = formatAbsoluteTime('2026-06-24T10:05:00.000Z', { locale: 'en-US' })
    expect(formatted).toContain('2026')
    expect(formatted).toMatch(/Jun/i)
  })
})
