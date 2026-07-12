import { describe, expect, it } from 'vitest'
import {
  buildOrgStorageBootstrapDownloadScript,
  buildOrgStorageBootstrapInvocation,
  bytesToGibi,
  gibiToBytes,
} from './orgStorageBootstrap'

describe('orgStorageBootstrap', () => {
  it('builds curl invocation with required args', () => {
    const command = buildOrgStorageBootstrapInvocation({
      enrollmentToken: 'secret-token',
      nodeId: 'org-storage-1',
      apiUrl: 'https://api.example.com/api',
      internalHost: 'storage.example.com',
    })

    expect(command).toContain('--token "secret-token"')
    expect(command).toContain('--node-id "org-storage-1"')
    expect(command).toContain('--api-url "https://api.example.com/api"')
    expect(command).toContain('--internal-host "storage.example.com"')
  })

  it('builds downloadable bootstrap script', () => {
    const script = buildOrgStorageBootstrapDownloadScript({
      enrollmentToken: 'secret-token',
      nodeId: 'org-storage-1',
      apiUrl: 'https://api.example.com/api',
      internalHost: 'storage.example.com',
    })

    expect(script.startsWith('#!/usr/bin/env bash')).toBe(true)
    expect(script).toContain('secret-token')
  })

  it('converts gibibytes to bytes', () => {
    expect(gibiToBytes(2)).toBe(2 * 1024 ** 3)
    expect(bytesToGibi(2 * 1024 ** 3)).toBe(2)
  })
})
