/** Placeholder for OpenAPI codegen; replaced when running `pnpm sync:api`. */
export const createClientConfig = (config: Record<string, unknown> = {}) => ({
  ...config,
  baseUrl: '/api',
  credentials: 'include' as RequestCredentials,
})

export type CreateClientConfig = typeof createClientConfig
