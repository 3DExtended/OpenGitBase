import type { Discussion, MergeRequestDiscussionLink, MergeRequestLinkType } from './api'

export const DISCUSSION_LINK_GROUP_ORDER: MergeRequestLinkType[] = ['closes', 'implements', 'related']

export function groupMergeRequestDiscussionLinks(
  links: MergeRequestDiscussionLink[],
  order: MergeRequestLinkType[] = DISCUSSION_LINK_GROUP_ORDER,
): Array<{ type: MergeRequestLinkType, links: MergeRequestDiscussionLink[] }> {
  return order
    .map(type => ({
      type,
      links: links.filter(link => link.relationshipType === type),
    }))
    .filter(group => group.links.length > 0)
}

export function filterLinkableDiscussions(
  discussions: Discussion[],
  linkedDiscussions: MergeRequestDiscussionLink[],
  search: string,
): Discussion[] {
  const linked = new Set(linkedDiscussions.map(link => link.discussionNumber))
  const query = search.trim().toLowerCase()

  return discussions
    .filter(discussion => !linked.has(discussion.number))
    .filter((discussion) => {
      if (!query) {
        return true
      }
      return discussion.title.toLowerCase().includes(query)
        || String(discussion.number).includes(query)
    })
}
