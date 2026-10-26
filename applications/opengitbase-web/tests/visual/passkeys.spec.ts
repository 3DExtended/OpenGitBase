import type { Page, Route } from '@playwright/test'
import { expect, test } from '@playwright/test'
import { blockServiceWorker, freezeClock, waitForPageReady } from './helpers'

const MOCK_USER = {
  userId: '22222222-2222-2222-2222-222222222222',
  username: 'demo-user',
  emailVerified: true,
  isAdmin: false,
  twoFactorEnabled: false,
}

interface StoredPasskey {
  id: string
  name: string
  createdAt: string
  lastUsedAt: string | null
  isBackedUp: boolean
  credentialId: string
}

function base64Url(bytes: Uint8Array): string {
  return Buffer.from(bytes).toString('base64url')
}

function clientData(encoded: string): { type: string, challenge: string, origin: string } {
  return JSON.parse(Buffer.from(encoded, 'base64url').toString('utf8'))
}

function json(route: Route, body: unknown, status = 200) {
  return route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) })
}

/**
 * A stand-in for the API that checks what Chromium's authenticator sends: the challenge it issued,
 * the page origin, and the ceremony type. Sensitive actions answer `reauthentication_required`
 * until the dialog has been completed, as the real API does.
 */
async function mockPasskeyApi(page: Page, origin: string) {
  const state = {
    passkeys: [] as StoredPasskey[],
    reauthenticated: false,
    challenge: '',
    reauthenticatedWith: [] as string[],
  }
  const issueChallenge = () => {
    state.challenge = base64Url(crypto.getRandomValues(new Uint8Array(16)))
    return state.challenge
  }
  const forbidden = (route: Route) => json(route, { error: 'reauthentication_required' }, 403)
  const publicView = ({ credentialId: _credentialId, ...passkey }: StoredPasskey) => passkey

  // Later routes win; this one only catches API calls the test does not expect.
  await page.route(url => url.pathname.startsWith('/api/'), route => json(route, { error: 'not mocked' }, 404))
  await page.route('**/api/account/me', route => json(route, MOCK_USER))
  await page.route('**/api/account/security', route => json(route, {
    twoFactorAvailable: true,
    twoFactorEnabled: false,
    totpConfigured: false,
    hasPassword: true,
    recoveryCodesRemaining: 0,
    passkeysAvailable: true,
  }))
  await page.route('**/api/account/reauthenticate', async (route) => {
    const body = route.request().postDataJSON() as { password?: string, passkeyAssertion?: { id: string, response: { clientDataJSON: string, userHandle?: string } } }
    if (body.passkeyAssertion) {
      const data = clientData(body.passkeyAssertion.response.clientDataJSON)
      expect(data).toMatchObject({ type: 'webauthn.get', challenge: state.challenge, origin })
      expect(state.passkeys.map(passkey => passkey.credentialId)).toContain(body.passkeyAssertion.id)
      expect(body.passkeyAssertion.response.userHandle).toBeTruthy()
      state.reauthenticatedWith.push('passkey')
    }
    else {
      expect(body.password).toBe('Password123!')
      state.reauthenticatedWith.push('password')
    }
    state.reauthenticated = true
    await route.fulfill({ status: 204 })
  })
  await page.route('**/api/account/reauthenticate/passkey-options', route => json(route, {
    challenge: issueChallenge(),
    rpId: 'localhost',
    allowCredentials: state.passkeys.map(passkey => ({ type: 'public-key', id: passkey.credentialId })),
    userVerification: 'required',
    timeout: 60000,
  }))
  await page.route('**/api/account/passkeys/*', async (route) => {
    const id = route.request().url().split('/').pop()
    const passkey = state.passkeys.find(candidate => candidate.id === id)
    if (!passkey) {
      return json(route, { error: 'not found' }, 404)
    }
    if (route.request().method() === 'PATCH') {
      passkey.name = (route.request().postDataJSON() as { name: string }).name
      return json(route, publicView(passkey))
    }
    if (!state.reauthenticated) {
      return forbidden(route)
    }
    state.passkeys = state.passkeys.filter(candidate => candidate !== passkey)
    return route.fulfill({ status: 204 })
  })
  await page.route('**/api/account/passkeys/registration-options', (route) => {
    if (!state.reauthenticated) {
      return forbidden(route)
    }
    return json(route, {
      rp: { id: 'localhost', name: 'OpenGitBase' },
      user: { id: base64Url(new Uint8Array(16).fill(0x22)), name: 'demo-user', displayName: 'demo-user' },
      challenge: issueChallenge(),
      pubKeyCredParams: [{ type: 'public-key', alg: -7 }],
      timeout: 60000,
      attestation: 'none',
      authenticatorSelection: { residentKey: 'required', requireResidentKey: true, userVerification: 'required' },
      excludeCredentials: state.passkeys.map(passkey => ({ type: 'public-key', id: passkey.credentialId })),
    })
  })
  await page.route('**/api/account/passkeys', async (route) => {
    if (route.request().method() === 'GET') {
      return json(route, state.passkeys.map(publicView))
    }
    const body = route.request().postDataJSON() as { name: string, attestationResponse: { id: string, rawId: string, type: string, response: { clientDataJSON: string, attestationObject: string } } }
    expect(clientData(body.attestationResponse.response.clientDataJSON)).toMatchObject({
      type: 'webauthn.create',
      challenge: state.challenge,
      origin,
    })
    expect(body.attestationResponse.type).toBe('public-key')
    expect(body.attestationResponse.rawId).toBe(body.attestationResponse.id)
    expect(body.attestationResponse.response.attestationObject).toBeTruthy()
    const passkey: StoredPasskey = {
      id: '77777777-7777-7777-7777-777777777777',
      name: body.name,
      createdAt: '2026-09-01T12:00:00Z',
      lastUsedAt: null,
      isBackedUp: false,
      credentialId: body.attestationResponse.id,
    }
    state.passkeys.push(passkey)
    return json(route, publicView(passkey), 201)
  })
  return state
}

async function waitForGallery(page: Page) {
  await page.goto('/__visual__/?msw=1')
  await waitForPageReady(page)
  await page.addStyleTag({
    content: `
      *, *::before, *::after {
        animation-duration: 0s !important;
        animation-delay: 0s !important;
        transition-duration: 0s !important;
        transition-delay: 0s !important;
      }
    `,
  })
}

test.describe('passkeys', () => {
  test('gallery passkeys section', async ({ page }) => {
    await freezeClock(page)
    await page.addInitScript(() => {
      document.cookie = 'ogb-site-gate-unlocked=1; Path=/; SameSite=Lax'
    })
    await waitForGallery(page)
    const section = page.getByTestId('visual-passkeys')
    await expect(section.getByTestId('passkeys-card').first().getByTestId('passkey-row')).toHaveCount(2)
    await expect(section.getByTestId('passkey-rename-input')).toHaveValue('YubiKey 5C')
    await expect(section).toHaveScreenshot('visual-passkeys.png')
  })

  test('gallery passkey sign-in with an error', async ({ page }) => {
    await page.addInitScript(() => {
      document.cookie = 'ogb-site-gate-unlocked=1; Path=/; SameSite=Lax'
    })
    await waitForGallery(page)
    const section = page.getByTestId('visual-passkey-sign-in')
    await expect(section.getByTestId('passkey-sign-in-error')).toBeVisible()
    await expect(section).toHaveScreenshot('visual-passkey-sign-in.png')
  })

  test('register, rename and delete a passkey with the virtual authenticator', async ({ page, baseURL }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop', 'One WebAuthn run is enough; the layout is covered by the gallery.')
    await blockServiceWorker(page)
    const origin = new URL(baseURL!).origin
    const api = await mockPasskeyApi(page, origin)
    const cdp = await page.context().newCDPSession(page)
    await cdp.send('WebAuthn.enable')
    const { authenticatorId } = await cdp.send('WebAuthn.addVirtualAuthenticator', {
      options: {
        protocol: 'ctap2',
        ctap2Version: 'ctap2_1',
        transport: 'internal',
        hasResidentKey: true,
        hasUserVerification: true,
        isUserVerified: true,
        automaticPresenceSimulation: true,
      },
    })

    await page.goto('/settings/security')
    const card = page.getByTestId('passkeys-card')
    await expect(card.getByTestId('passkeys-empty')).toBeVisible()

    // Adding needs a recent re-authentication: the dialog asks for the password, then the ceremony runs.
    await card.getByTestId('passkey-add').click()
    await card.getByTestId('passkey-name-input').fill('Work laptop')
    await card.getByTestId('passkey-create').click()
    const dialog = page.getByTestId('reauthenticate-form')
    await expect(dialog).toBeVisible()
    await expect(dialog.getByTestId('reauth-use-passkey')).toHaveCount(0)
    await dialog.locator('input[type="password"]').fill('Password123!')
    await dialog.getByRole('button', { name: 'Confirm' }).click()

    await expect(card.getByTestId('passkey-name')).toHaveText(['Work laptop'])
    await expect(page.getByText('Passkey added.')).toBeVisible()
    const { credentials } = await cdp.send('WebAuthn.getCredentials', { authenticatorId })
    expect(credentials).toHaveLength(1)
    expect(credentials[0]?.isResidentCredential).toBe(true)
    expect(base64Url(Buffer.from(credentials[0]!.credentialId, 'base64'))).toBe(api.passkeys[0]?.credentialId)

    await card.getByTestId('passkey-rename').click()
    await card.getByTestId('passkey-rename-input').fill('Desk laptop')
    await card.getByTestId('passkey-rename-form').getByRole('button', { name: 'Save' }).click()
    await expect(card.getByTestId('passkey-name')).toHaveText(['Desk laptop'])
    expect(api.passkeys[0]?.name).toBe('Desk laptop')

    // The re-authentication has lapsed: this time the dialog confirms with the new passkey.
    api.reauthenticated = false
    await card.getByTestId('passkey-remove').click()
    await page.getByTestId('passkey-remove-confirm').click()
    await expect(dialog).toBeVisible()
    await dialog.getByTestId('reauth-use-passkey').click()

    await expect(card.getByTestId('passkeys-empty')).toBeVisible()
    await expect(page.getByText('Passkey removed.')).toBeVisible()
    expect(api.reauthenticatedWith).toEqual(['password', 'passkey'])
    expect(api.passkeys).toHaveLength(0)
  })
})
