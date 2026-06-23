/** PROTOTYPE — dev-only discussion UI; delete when promoting prototype to production. */
export function useDiscussionPrototypeEnabled(): ComputedRef<boolean> {
  return computed(() => import.meta.dev)
}
