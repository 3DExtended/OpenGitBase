import { describe, expect, it } from 'vitest'
import { inferCodeLanguage } from './codeLanguage'
import { resolveHighlightLanguage, themeForColorMode } from './codeHighlight'

describe('inferCodeLanguage', () => {
  it('detects JSON object and array literals', () => {
    expect(inferCodeLanguage('{"a": 1}')).toBe('json')
    expect(inferCodeLanguage('[1, 2]')).toBe('json')
  })

  it('returns null for non-json content', () => {
    expect(inferCodeLanguage('const x = 1')).toBeNull()
    expect(inferCodeLanguage('{ not json')).toBeNull()
  })
})

describe('resolveHighlightLanguage', () => {
  it('prefers explicit non-text overrides', () => {
    expect(resolveHighlightLanguage('code', 'file.ts', 'typescript')).toBe('typescript')
  })

  it('infers json for untagged fences', () => {
    expect(resolveHighlightLanguage('{"a":1}', 'snippet.txt')).toBe('json')
  })

  it('falls back to file path language', () => {
    expect(resolveHighlightLanguage('{}', '.agentGenCli.json')).toBe('json')
  })
})

describe('themeForColorMode', () => {
  it('maps explicit modes', () => {
    expect(themeForColorMode('dark')).toBe('github-dark')
    expect(themeForColorMode('light')).toBe('github-light')
  })
})
