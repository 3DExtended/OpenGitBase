import { describe, expect, it } from 'vitest'

import { communityPitchSlides } from './communityPitchSlides'

describe('communityPitchSlides', () => {
  it('includes title, contribution lanes, and both CTAs', () => {
    const ids = communityPitchSlides.map(slide => slide.id)

    expect(ids).toContain('title')
    expect(ids).toContain('contribute-lanes')
    expect(ids).toContain('cta-contribute')
    expect(ids).toContain('cta-run')
    expect(ids).toContain('where-we-are')
  })

  it('ends with contribute before run locally', () => {
    const ids = communityPitchSlides.map(slide => slide.id)
    expect(ids.indexOf('cta-contribute')).toBeLessThan(ids.indexOf('cta-run'))
  })

  it('uses unique slide ids for reveal hash navigation', () => {
    const ids = communityPitchSlides.map(slide => slide.id)
    expect(new Set(ids).size).toBe(ids.length)
  })
})
