import { defineConfig } from '@hey-api/openapi-ts'

export default defineConfig({
  input: './openapi/swagger.json',
  output: {
    path: './generated/api',
    lint: 'eslint',
  },
  plugins: [
    {
      name: '@hey-api/client-fetch',
      runtimeConfigPath: './app/utils/api-client-config.ts',
    },
  ],
})
