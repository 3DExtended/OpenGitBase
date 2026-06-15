export function useInstanceBranding() {
  const config = useRuntimeConfig()

  const instanceName = computed(() => config.public.instanceName as string)
  const instanceLogoUrl = computed(() => config.public.instanceLogoUrl as string)
  const apiBase = computed(() => config.public.apiBase as string)

  return {
    instanceName,
    instanceLogoUrl,
    apiBase,
  }
}
