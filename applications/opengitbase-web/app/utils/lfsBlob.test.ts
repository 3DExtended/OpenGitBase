import { afterEach, describe, expect, it, vi } from 'vitest'
import { createApi } from './api'
import type { RepositoryContentBlob } from './api'
import { LFS_IMAGE_PREVIEW_MAX_BYTES, lfsBlobView } from './lfsBlob'

const oid = '4d7a214614ab2935c943f9e0ff69d22eadbb8f32b1258daaa5e2ca24d17e2393'

function blob(overrides: Partial<RepositoryContentBlob>): RepositoryContentBlob {
  return {
    ref: 'main',
    path: 'assets/logo.png',
    size: 4096,
    isBinary: true,
    isTooLarge: false,
    previewKind: 'image',
    textContent: null,
    contentBase64: null,
    lfs: { oid, size: 4096, available: true },
    replicationLag: null,
    ...overrides,
  }
}

function jsonResponse(body: unknown): Response {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } })
}

describe('lfsBlobView', () => {
  it('is null for a blob that is not stored with Git LFS', () => {
    expect(lfsBlobView(blob({ lfs: null }))).toBeNull()
  })

  it('previews an available LFS image inline', () => {
    expect(lfsBlobView(blob({}))).toBe('image')
  })

  it('offers a download for other LFS files, text included', () => {
    expect(lfsBlobView(blob({ path: 'data/model.bin', previewKind: 'binary' }))).toBe('download')
    expect(lfsBlobView(blob({ path: 'notes.txt', previewKind: 'text' }))).toBe('download')
    expect(lfsBlobView(blob({ path: 'drawing.svg', previewKind: 'svg' }))).toBe('download')
  })

  it('offers a download for an LFS image too large to preview', () => {
    const size = LFS_IMAGE_PREVIEW_MAX_BYTES + 1
    expect(lfsBlobView(blob({ size, isTooLarge: true, lfs: { oid, size, available: true } }))).toBe('download')
  })

  it('reports an LFS object the repository never received as unavailable', () => {
    expect(lfsBlobView(blob({ lfs: { oid, size: 4096, available: false } }))).toBe('unavailable')
  })
})

describe('repository content API and LFS', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('reads the LFS object of a pointer blob', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(jsonResponse({
      ref: 'main',
      path: 'assets/logo.png',
      size: 4096,
      isBinary: true,
      isTooLarge: false,
      previewKind: 'image',
      lfs: { oid, size: 4096, available: true },
    })))

    const result = await createApi('/api').repositoryContent.getBlob('acme', 'demo', 'main', 'assets/logo.png')

    expect(result.data?.lfs).toEqual({ oid, size: 4096, available: true })
    expect(result.data?.textContent).toBeNull()
  })

  it('leaves lfs empty for an ordinary blob', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(jsonResponse({
      ref: 'main',
      path: 'README.md',
      size: 6,
      previewKind: 'text',
      textContent: '# Hi\n',
    })))

    const result = await createApi('/api').repositoryContent.getBlob('acme', 'demo', 'main', 'README.md')

    expect(result.data?.lfs).toBeNull()
  })

  it('reads git and LFS bytes of the repository usage', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(jsonResponse({
      bytesUsed: 3_145_728,
      bytesLimit: 1_073_741_824,
      fileSizeLimit: 52_428_800,
      gitBytes: 1_048_576,
      lfsBytes: 2_097_152,
    })))

    const result = await createApi('/api').repositories.usage('repo-1')

    expect(result.data).toMatchObject({ bytesUsed: 3_145_728, gitBytes: 1_048_576, lfsBytes: 2_097_152 })
  })
})
