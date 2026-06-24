import { describe, expect, it } from 'vitest'
import { visualDiscussionSubThreads } from './discussionSubThreadVisualFixtures'

describe('visualDiscussionSubThreads fixtures', () => {
  it('provides nested reply data', () => {
    expect(visualDiscussionSubThreads.open.replies).toHaveLength(1)
    expect(visualDiscussionSubThreads.resolved.isResolved).toBe(true)
    expect(visualDiscussionSubThreads.orphan.orphanedFromDeletedRoot).toBe(true)
  })
})
