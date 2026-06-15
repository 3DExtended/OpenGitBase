#!/usr/bin/env node
/**
 * Build the .NET API, export OpenAPI spec, and regenerate the TypeScript client.
 *
 * Usage (from repo root):
 *   node scripts/sync-openapi.mjs
 *
 * Or from the web app:
 *   pnpm sync:api
 */

import { spawnSync } from 'node:child_process'
import { existsSync, mkdirSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const __dirname = dirname(fileURLToPath(import.meta.url))
const repoRoot = resolve(__dirname, '..')
const apiProject = join(repoRoot, 'applications/OpenGitBase.Api/OpenGitBase.Api.csproj')
const webAppDir = join(repoRoot, 'applications/opengitbase-web')
const openapiDir = join(webAppDir, 'openapi')
const swaggerOutput = join(openapiDir, 'swagger.json')
const swaggerDocName = 'v1.0'

function run(command, args, options = {}) {
  const result = spawnSync(command, args, {
    stdio: 'inherit',
    cwd: repoRoot,
    ...options,
  })

  if (result.status !== 0) {
    process.exit(result.status ?? 1)
  }
}

function findApiDll() {
  const configurations = ['Debug', 'Release']

  for (const configuration of configurations) {
    const dllPath = join(
      repoRoot,
      'applications/OpenGitBase.Api/bin',
      configuration,
      'net10.0/OpenGitBase.Api.dll',
    )

    if (existsSync(dllPath)) {
      return dllPath
    }
  }

  console.error('Could not find OpenGitBase.Api.dll after build.')
  process.exit(1)
}

console.log('Restoring .NET tools…')
run('dotnet', ['tool', 'restore'])

console.log('Building OpenGitBase.Api…')
run('dotnet', [
  'build',
  apiProject,
  '--configuration',
  'Release',
  '/p:RunAnalyzersDuringBuild=false',
])

const apiDll = findApiDll()

mkdirSync(openapiDir, { recursive: true })

console.log(`Exporting OpenAPI spec (${swaggerDocName})…`)
run('dotnet', [
  'swagger',
  'tofile',
  '--output',
  swaggerOutput,
  apiDll,
  swaggerDocName,
])

console.log('Generating TypeScript API client…')
run('pnpm', ['exec', 'openapi-ts'], { cwd: webAppDir })

console.log('OpenAPI sync complete.')
