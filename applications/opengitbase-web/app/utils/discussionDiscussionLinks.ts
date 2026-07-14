import type { Discussion, DiscussionDiscussionLink, DiscussionLinkType } from './api'

export const DISCUSSION_LINK_GROUP_ORDER: DiscussionLinkType[] = [
  'parent',
  'child',
  'blocks',
  'related',
]

export function groupDiscussionDiscussionLinks(
  links: DiscussionDiscussionLink[],
  order: DiscussionLinkType[] = DISCUSSION_LINK_GROUP_ORDER,
): Array<{ type: DiscussionLinkType, links: DiscussionDiscussionLink[] }> {
  const grouped = new Map<DiscussionLinkType, DiscussionDiscussionLink[]>()
  for (const link of links) {
    const bucket = grouped.get(link.relationshipType) ?? []
    bucket.push(link)
    grouped.set(link.relationshipType, bucket)
  }

  return order
    .filter(type => grouped.has(type))
    .map(type => ({
      type,
      links: grouped.get(type) ?? [],
    }))
}

export function filterLinkableDiscussions(
  discussions: Discussion[],
  linkedDiscussions: DiscussionDiscussionLink[],
  search: string,
): Discussion[] {
  const linked = new Set(linkedDiscussions.map(link => link.targetDiscussionNumber))
  const query = search.trim().toLowerCase()
  return discussions
    .filter(discussion => !linked.has(discussion.number))
    .filter(discussion =>
      !query
      || discussion.title.toLowerCase().includes(query)
      || String(discussion.number).includes(query))
}
