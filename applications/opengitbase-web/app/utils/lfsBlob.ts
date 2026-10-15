import type { RepositoryContentBlob } from './api'

/** LFS images up to this size are previewed inline from the raw endpoint; larger ones are download only. */
export const LFS_IMAGE_PREVIEW_MAX_BYTES = 20 * 1024 * 1024

/**
 * How the blob view shows a file stored with Git LFS, or null for an ordinary blob. LFS text files are not
 * rendered inline (v1): they are offered as a download like any other LFS file.
 */
export type LfsBlobView = 'image' | 'download' | 'unavailable'

export function lfsBlobView(blob: RepositoryContentBlob): LfsBlobView | null {
  if (!blob.lfs) {
    return null
  }
  if (!blob.lfs.available) {
    return 'unavailable'
  }
  if (blob.previewKind === 'image' && blob.lfs.size <= LFS_IMAGE_PREVIEW_MAX_BYTES) {
    return 'image'
  }
  return 'download'
}
