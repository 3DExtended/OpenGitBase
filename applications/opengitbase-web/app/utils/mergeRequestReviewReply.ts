import type {
  CommentAnchorInput,
  DiffSide,
  MergeRequestCommentAnchor,
} from '~/utils/api'

export function resolveReviewReplyAnchor(
  attachedAnchor: CommentAnchorInput | null,
  lineDiffSide: DiffSide,
): MergeRequestCommentAnchor | null {
  if (!attachedAnchor) {
    return null
  }

  return {
    headCommitSha: attachedAnchor.commitSha,
    filePath: attachedAnchor.filePath,
    lineNumber: attachedAnchor.line,
    diffSide: lineDiffSide,
  }
}
