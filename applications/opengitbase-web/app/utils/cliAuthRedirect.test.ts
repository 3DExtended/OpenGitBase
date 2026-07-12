import { describe, expect, it } from 'vitest'
import { buildCliAuthCallbackUrl, parseCliAuthQueryParams } from './cliAuthRedirect'

describe('parseCliAuthQueryParams', () => {
  it('accepts valid port and state', () => {
    expect(parseCliAuthQueryParams('54321', 'abc-state')).toEqual({
      port: '54321',
      state: 'abc-state',
    })
  })

  it('rejects missing params', () => {
    expect(parseCliAuthQueryParams(undefined, 'abc')).toBeNull()
    expect(parseCliAuthQueryParams('1234', undefined)).toBeNull()
  })

  it('rejects invalid port values', () => {
    expect(parseCliAuthQueryParams('0', 'abc')).toBeNull()
    expect(parseCliAuthQueryParams('70000', 'abc')).toBeNull()
    expect(parseCliAuthQueryParams('abc', 'abc')).toBeNull()
  })
})

describe('buildCliAuthCallbackUrl', () => {
  it('builds localhost callback with encoded token and state', () => {
    const url = buildCliAuthCallbackUrl('54321', 'state+value', 'jwt.token+value')
    expect(url).toBe(
      'http://127.0.0.1:54321/callback?token=jwt.token%2Bvalue&state=state%2Bvalue',
    )
  })
})
