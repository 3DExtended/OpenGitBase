export default defineNuxtRouteMiddleware((to) => {
  if (!import.meta.dev && to.path.startsWith('/__visual__')) {
    return navigateTo('/')
  }
})
