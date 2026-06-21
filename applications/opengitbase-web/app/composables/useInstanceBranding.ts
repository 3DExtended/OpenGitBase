export function useInstanceBranding() {
  const config = useRuntimeConfig()

  const instanceName = computed(() => config.public.instanceName as string)
  const instanceLogoUrl = computed(() => config.public.instanceLogoUrl as string)
  const apiBase = computed(() => config.public.apiBase as string)
  const deployVersion = computed(() => config.public.deployVersion as string)
  const deploySha = computed(() => config.public.deploySha as string)

  return {
    instanceName,
    instanceLogoUrl,
    apiBase,
    deployVersion,
    deploySha,
  }
}
