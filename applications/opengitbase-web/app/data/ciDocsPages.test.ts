import { describe, expect, it } from 'vitest'
import { getCiDocPage, getCiDocSlugs, ciDocsPages } from './ciDocsPages'

describe('ciDocsPages', () => {
  it('has unique slugs and resolvable pages', () => {
    const slugs = getCiDocSlugs()
    expect(new Set(slugs).size).toBe(slugs.length)
    expect(slugs.length).toBeGreaterThan(5)

    for (const slug of slugs) {
      const page = getCiDocPage(slug)
      expect(page).toBeDefined()
      expect(page?.markdown.trim().length).toBeGreaterThan(20)
      expect(page?.title.trim().length).toBeGreaterThan(0)
    }
  })

  it('starts with overview quick-start and how-it-works', () => {
    expect(ciDocsPages[0]?.slug).toBe('overview')
    expect(ciDocsPages[1]?.slug).toBe('quick-start')
    expect(ciDocsPages[2]?.slug).toBe('how-it-works')
  })
})
