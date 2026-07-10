import { isMswEnabled } from '~/utils/mswEnabled'

export default defineNuxtPlugin(async () => {
  const config = useRuntimeConfig()

  const mswEnabled = isMswEnabled(String(config.public.msw))

  if (import.meta.client && 'serviceWorker' in navigator && !mswEnabled) {
    const registrations = await navigator.serviceWorker.getRegistrations()
    await Promise.all(
      registrations
        .filter(reg => reg.active?.scriptURL.includes('mockServiceWorker'))
        .map(reg => reg.unregister()),
    )
  }

  if (!import.meta.client || !mswEnabled) {
    return
  }

  const { worker } = await import('../../tests/mocks/browser')
  await worker.start({
    onUnhandledRequest: 'bypass',
    quiet: true,
    serviceWorker: {
      url: '/mockServiceWorker.js',
    },
  })
})
