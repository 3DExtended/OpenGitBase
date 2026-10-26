/** English fallback for thread headers when no localized reply-count label is passed. */
export function formatReplyCount(count: number): string {
  return `${count} ${count === 1 ? 'reply' : 'replies'}`
}
