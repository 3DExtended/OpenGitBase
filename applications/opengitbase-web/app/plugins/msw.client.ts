export default defineNuxtPlugin(async () => {
  const config = useRuntimeConfig()

  const mswEnabled = config.public.msw === 'true'
    || (import.meta.client && window.location.search.includes('msw=1'))

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
