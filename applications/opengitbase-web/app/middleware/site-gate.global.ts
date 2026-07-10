export default defineNuxtRouteMiddleware((to) => {
  const config = useRuntimeConfig()

  if (!config.public.siteGateEnabled || !import.meta.dev) {
    return
  }

  if (to.path === '/gate') {
    return
  }

  if (isSiteGateUnlocked()) {
    return
  }

  return navigateTo({
    path: '/gate',
    query: { redirect: to.fullPath },
  })
})
