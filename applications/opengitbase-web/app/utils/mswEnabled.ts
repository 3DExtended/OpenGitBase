export type MswEnableContext = {
  isDev: boolean
  mswPublicConfig: string
  hasMswQuery: boolean
}

export function resolveMswEnabled(context: MswEnableContext): boolean {
  if (!context.isDev) {
    return false
  }

  if (context.hasMswQuery) {
    return true
  }

  return context.mswPublicConfig === 'true'
}

/**
 * MSW may only run in development builds. Production must never activate mocks,
 * including via ?msw=1 query string bypass.
 */
export function isMswEnabled(mswPublicConfig: string): boolean {
  return resolveMswEnabled({
    isDev: import.meta.dev,
    mswPublicConfig,
    hasMswQuery: import.meta.client && window.location.search.includes('msw=1'),
  })
}
