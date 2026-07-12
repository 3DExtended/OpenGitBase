import { describe, expect, it } from 'vitest'
import { getStorageDocPage, getStorageDocSlugs, storageDocsPages } from './storageDocsPages'

describe('storageDocsPages', () => {
  it('has unique slugs and resolvable pages', () => {
    const slugs = getStorageDocSlugs()
    expect(new Set(slugs).size).toBe(slugs.length)
    expect(slugs.length).toBeGreaterThan(0)

    for (const slug of slugs) {
      const page = getStorageDocPage(slug)
      expect(page).toBeDefined()
      expect(page?.markdown.trim().length).toBeGreaterThan(20)
      expect(page?.title.trim().length).toBeGreaterThan(0)
    }
  })

  it('includes org storage nodes guide', () => {
    expect(storageDocsPages[0]?.slug).toBe('org-storage-nodes')
  })
})
