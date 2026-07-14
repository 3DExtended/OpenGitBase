import { describe, expect, it } from 'vitest'
import type { Discussion, DiscussionDiscussionLink } from './api'
import {
  filterLinkableDiscussions,
  groupDiscussionDiscussionLinks,
} from './discussionDiscussionLinks'

function link(
  targetDiscussionNumber: number,
  relationshipType: DiscussionDiscussionLink['relationshipType'],
  title = 'Linked',
): DiscussionDiscussionLink {
  return {
    targetDiscussionNumber,
    relationshipType,
    targetDiscussionTitle: title,
    targetDiscussionStatus: 'Open',
  }
}

describe('groupDiscussionDiscussionLinks', () => {
  it('groups links by relationship type in display order', () => {
    const grouped = groupDiscussionDiscussionLinks([
      link(2, 'related'),
      link(3, 'parent'),
      link(4, 'blocks'),
    ])

    expect(grouped.map(group => group.type)).toEqual(['parent', 'blocks', 'related'])
  })
})

describe('filterLinkableDiscussions', () => {
  it('excludes already linked discussions and applies search', () => {
    const discussions: Discussion[] = [
      { number: 1, title: 'PRD spec' } as Discussion,
      { number: 2, title: 'Slice one' } as Discussion,
      { number: 3, title: 'Other topic' } as Discussion,
    ]

    const filtered = filterLinkableDiscussions(
      discussions,
      [link(2, 'child')],
      'other',
    )

    expect(filtered.map(d => d.number)).toEqual([3])
  })
})
