import { describe, expect, it } from 'vitest'
import { createI18n } from 'vue-i18n'
import en from '../../i18n/locales/en.json'
import { formatReplyCount } from './replyCount'

describe('repo.discussions.replyCount (en)', () => {
  const { t } = createI18n({ legacy: false, locale: 'en', messages: { en } }).global

  it('uses the singular form for one reply', () => {
    expect(t('repo.discussions.replyCount', 1)).toBe('1 reply')
  })

  it('uses the plural form for several replies', () => {
    expect(t('repo.discussions.replyCount', 2)).toBe('2 replies')
  })
})

describe('formatReplyCount', () => {
  it('uses the singular form for one reply', () => {
    expect(formatReplyCount(1)).toBe('1 reply')
  })

  it('uses the plural form for several replies', () => {
    expect(formatReplyCount(2)).toBe('2 replies')
  })
})
