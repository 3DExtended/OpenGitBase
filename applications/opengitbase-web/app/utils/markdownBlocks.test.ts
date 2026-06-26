import { describe, expect, it } from 'vitest'
import { splitMarkdownByFences } from './markdownBlocks'

describe('splitMarkdownByFences', () => {
  it('splits prose and fenced code blocks', () => {
    const source = [
      'Intro text',
      '',
      '```typescript',
      'const x = 1',
      '```',
      '',
      'Outro',
    ].join('\n')

    expect(splitMarkdownByFences(source)).toEqual([
      { type: 'prose', markdown: 'Intro text\n' },
      { type: 'code', language: 'typescript', source: 'const x = 1' },
      { type: 'prose', markdown: '\nOutro' },
    ])
  })

  it('handles a document that is only a code fence', () => {
    expect(splitMarkdownByFences('```js\nalert(1)\n```')).toEqual([
      { type: 'code', language: 'js', source: 'alert(1)' },
    ])
  })

  it('returns prose-only markdown unchanged', () => {
    const source = 'Just **markdown**'
    expect(splitMarkdownByFences(source)).toEqual([
      { type: 'prose', markdown: source },
    ])
  })
})
