import { describe, expect, it } from 'vitest'
import type { Discussion, MergeRequestDiscussionLink } from './api'
import {
  DISCUSSION_LINK_GROUP_ORDER,
  filterLinkableDiscussions,
  groupMergeRequestDiscussionLinks,
} from './mergeRequestDiscussionLinks'

function discussion(number: number, title: string): Discussion {
  return {
    id: `disc-${number}`,
    repositoryId: 'repo-1',
    number,
    title,
    status: 'Open',
    hasEverBeenEngaged: false,
    creatorUserId: 'user-1',
    createdAt: '2026-06-27T08:00:00.000Z',
    updatedAt: '2026-06-27T08:00:00.000Z',
    tags: [],
  }
}

function link(
  discussionNumber: number,
  relationshipType: MergeRequestDiscussionLink['relationshipType'],
  title: string,
): MergeRequestDiscussionLink {
  return {
    discussionNumber,
    relationshipType,
    discussionTitle: title,
    discussionStatus: 'Open',
  }
}

describe('groupMergeRequestDiscussionLinks', () => {
  it('groups links by relationship in closes → implements → related order', () => {
    const grouped = groupMergeRequestDiscussionLinks([
      link(3, 'related', 'Docs'),
      link(1, 'closes', 'Auth'),
      link(2, 'implements', 'API'),
    ])

    expect(grouped.map(group => group.type)).toEqual(['closes', 'implements', 'related'])
    expect(grouped[0]?.links[0]?.discussionNumber).toBe(1)
    expect(grouped[1]?.links[0]?.discussionNumber).toBe(2)
    expect(grouped[2]?.links[0]?.discussionNumber).toBe(3)
  })

  it('omits empty relationship groups', () => {
    const grouped = groupMergeRequestDiscussionLinks([
      link(4, 'closes', 'Only closes'),
    ])

    expect(grouped).toHaveLength(1)
    expect(grouped[0]?.type).toBe('closes')
  })

  it('uses the default group order constant', () => {
    expect(DISCUSSION_LINK_GROUP_ORDER).toEqual(['closes', 'implements', 'related'])
  })
})

describe('filterLinkableDiscussions', () => {
  const openDiscussions = [
    discussion(1, 'Auth refactor'),
    discussion(2, 'Add integration tests'),
    discussion(3, 'README cleanup'),
  ]

  it('excludes already linked discussions', () => {
    const filtered = filterLinkableDiscussions(
      openDiscussions,
      [link(1, 'closes', 'Auth refactor')],
      '',
    )

    expect(filtered.map(item => item.number)).toEqual([2, 3])
  })

  it('filters by title or number', () => {
    const filtered = filterLinkableDiscussions(openDiscussions, [], 'integration')

    expect(filtered).toHaveLength(1)
    expect(filtered[0]?.number).toBe(2)
  })

  it('matches discussion numbers in the search query', () => {
    const filtered = filterLinkableDiscussions(openDiscussions, [], '3')

    expect(filtered).toHaveLength(1)
    expect(filtered[0]?.number).toBe(3)
  })
})
