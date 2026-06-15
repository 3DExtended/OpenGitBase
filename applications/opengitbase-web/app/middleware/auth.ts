export default defineNuxtRouteMiddleware(async (to) => {
  const auth = useAuthStore()

  if (!auth.initialized) {
    await auth.fetchMe()
  }

  if (!auth.isAuthenticated) {
    return navigateTo({
      path: '/sign-in',
      query: { redirect: to.fullPath },
    })
  }
})
