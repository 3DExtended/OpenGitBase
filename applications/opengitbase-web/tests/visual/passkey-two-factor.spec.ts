import { generateKeyPairSync, randomBytes } from 'node:crypto'
import type { CDPSession, Page, Route } from '@playwright/test'
import { PAGE_READY_TIMEOUT, blockServiceWorker, expect, test } from './helpers'

const MOCK_USER = {
  userId: '22222222-2222-2222-2222-222222222222',
  username: 'demo-user',
  emailVerified: true,
  isAdmin: false,
  twoFactorEnabled: false,
}

const CHALLENGE_TOKEN = 'sign-in-challenge'

interface Assertion {
  id: string
  type: string
  response: { clientDataJSON: string, authenticatorData: string, signature: string }
}

function json(route: Route, body: unknown, status = 200) {
  return route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) })
}

function clientData(encoded: string): { type: string, challenge: string, origin: string } {
  return JSON.parse(Buffer.from(encoded, 'base64url').toString('utf8'))
}

/** The flags byte after the RP ID hash: bit 0 user present, bit 2 user verified. */
function flags(assertion: Assertion): { present: boolean, verified: boolean } {
  const byte = Buffer.from(assertion.response.authenticatorData, 'base64url')[32] ?? 0
  return { present: (byte & 0x01) !== 0, verified: (byte & 0x04) !== 0 }
}

/**
 * A security key on USB: it holds a passkey for `localhost` but cannot verify the user, so it only
 * proves presence. That is enough after the password.
 */
async function addSecurityKey(cdp: CDPSession, credentialId: Buffer) {
  await cdp.send('WebAuthn.enable')
  const { authenticatorId } = await cdp.send('WebAuthn.addVirtualAuthenticator', {
    options: {
      protocol: 'ctap2',
      ctap2Version: 'ctap2_1',
      transport: 'usb',
      hasResidentKey: false,
      hasUserVerification: false,
      automaticPresenceSimulation: true,
    },
  })
  const { privateKey } = generateKeyPairSync('ec', { namedCurve: 'P-256' })
  await cdp.send('WebAuthn.addCredential', {
    authenticatorId,
    credential: {
      credentialId: credentialId.toString('base64'),
      isResidentCredential: false,
      rpId: 'localhost',
      privateKey: privateKey.export({ format: 'der', type: 'pkcs8' }).toString('base64'),
      signCount: 0,
    },
  })
  return authenticatorId
}

/** Without conditional mediation the sign-in page starts no passkey request of its own. */
async function withoutAutofill(page: Page) {
  await page.addInitScript(() => {
    Object.defineProperty(PublicKeyCredential, 'isConditionalMediationAvailable', { value: async () => false })
  })
}

/**
 * A stand-in for the two-step sign-in: the password answers with a challenge, the passkey options
 * are bound to it, and the second step checks what Chromium's authenticator signed.
 */
async function mockTwoStepSignIn(page: Page, origin: string, credentialId: Buffer, methods: string[]) {
  const state = { signedIn: false, challenge: '', optionsTokens: [] as string[], answers: [] as Assertion[] }

  await page.route(url => url.pathname.startsWith('/api/'), route => json(route, { error: 'not mocked' }, 404))
  await page.route('**/api/account/me', route =>
    state.signedIn ? json(route, { ...MOCK_USER, twoFactorEnabled: true }) : json(route, { error: 'unauthorized' }, 401))
  await page.route('**/api/signin/login', (route) => {
    expect(route.request().postDataJSON()).toEqual({ username: 'demo-user', password: 'Password123!' })
    return json(route, { twoFactorRequired: true, challengeToken: CHALLENGE_TOKEN, methods })
  })
  await page.route('**/api/signin/two-factor/passkey-options', (route) => {
    state.optionsTokens.push((route.request().postDataJSON() as { challengeToken: string }).challengeToken)
    state.challenge = randomBytes(16).toString('base64url')
    return json(route, {
      challenge: state.challenge,
      rpId: 'localhost',
      allowCredentials: [{ type: 'public-key', id: credentialId.toString('base64url') }],
      userVerification: 'preferred',
      timeout: 60000,
    })
  })
  await page.route('**/api/signin/two-factor', (route) => {
    const body = route.request().postDataJSON() as { challengeToken: string, passkeyAssertion: Assertion }
    expect(body.challengeToken).toBe(CHALLENGE_TOKEN)
    expect(clientData(body.passkeyAssertion.response.clientDataJSON)).toMatchObject({
      type: 'webauthn.get',
      challenge: state.challenge,
      origin,
    })
    expect(body.passkeyAssertion.id).toBe(credentialId.toString('base64url'))
    state.answers.push(body.passkeyAssertion)
    state.signedIn = true
    return route.fulfill({ status: 200, contentType: 'text/plain', body: 'session-token' })
  })
  return state
}

async function signInWithPassword(page: Page) {
  await page.getByRole('textbox').first().fill('demo-user')
  await page.locator('input[type="password"]').fill('Password123!')
  await page.getByRole('button', { name: 'Sign in', exact: true }).click()
  await expect(page.getByTestId('two-factor-code-form')).toBeVisible()
}

test.describe('passkey as second factor', () => {
  test.beforeEach(({ page: _page }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop', 'One WebAuthn run is enough; the layout is covered by snapshots.')
  })

  test('a security key touch completes the second step after the password', async ({ page, baseURL }) => {
    await withoutAutofill(page)
    await blockServiceWorker(page)
    const credentialId = randomBytes(16)
    const api = await mockTwoStepSignIn(page, new URL(baseURL!).origin, credentialId, ['totp', 'passkey', 'recovery'])
    await addSecurityKey(await page.context().newCDPSession(page), credentialId)

    await page.goto('/sign-in?redirect=/settings')
    await signInWithPassword(page)
    // The authenticator code stays the main field; the passkey is offered next to it.
    await expect(page.getByLabel('Authentication code')).toBeVisible()
    await page.getByTestId('two-factor-use-passkey').click()

    await expect.poll(() => new URL(page.url()).pathname, { timeout: PAGE_READY_TIMEOUT }).toBe('/settings')
    expect(api.optionsTokens).toEqual([CHALLENGE_TOKEN])
    expect(api.answers).toHaveLength(1)
    expect(flags(api.answers[0]!)).toEqual({ present: true, verified: false })
  })

  test('passkey-only 2FA offers the passkey and no code field', async ({ page, baseURL }) => {
    await withoutAutofill(page)
    await blockServiceWorker(page)
    const credentialId = randomBytes(16)
    const api = await mockTwoStepSignIn(page, new URL(baseURL!).origin, credentialId, ['passkey', 'recovery'])
    await addSecurityKey(await page.context().newCDPSession(page), credentialId)

    await page.goto('/sign-in?redirect=/settings')
    await signInWithPassword(page)
    await expect(page.getByLabel('Authentication code')).toHaveCount(0)
    await expect(page.getByTestId('two-factor-toggle-method')).toHaveText('Use a recovery code')
    await page.getByTestId('two-factor-use-passkey').click()

    await expect.poll(() => new URL(page.url()).pathname, { timeout: PAGE_READY_TIMEOUT }).toBe('/settings')
    expect(api.signedIn).toBe(true)
  })

  test('the CLI page hands the two-step session to the CLI', async ({ page, baseURL }) => {
    await withoutAutofill(page)
    await blockServiceWorker(page)
    const credentialId = randomBytes(16)
    const api = await mockTwoStepSignIn(page, new URL(baseURL!).origin, credentialId, ['totp', 'passkey'])
    await addSecurityKey(await page.context().newCDPSession(page), credentialId)
    const callback = page.waitForRequest(request => new URL(request.url()).hostname === '127.0.0.1')
    await page.route('http://127.0.0.1:54321/**', route => route.fulfill({ status: 200, contentType: 'text/plain', body: 'done' }))

    await page.goto('/cli/auth?port=54321&state=cli-state')
    await signInWithPassword(page)
    await page.getByTestId('two-factor-use-passkey').click()

    const url = new URL((await callback).url())
    expect(url.pathname).toBe('/callback')
    expect(url.searchParams.get('token')).toBe('session-token')
    expect(api.signedIn).toBe(true)
  })

  test('turn on 2FA with passkeys, then keep the last passkey', async ({ page, baseURL }) => {
    await blockServiceWorker(page)
    const origin = new URL(baseURL!).origin
    const credentialId = randomBytes(16)
    await addSecurityKey(await page.context().newCDPSession(page), credentialId)
    const recoveryCodes = Array.from({ length: 10 }, (_, index) => `code${index}-abcde`)
    const state = { twoFactorEnabled: false, reauthenticated: false, challenge: '', reauthentications: [] as string[] }
    const passkey = {
      id: '77777777-7777-7777-7777-777777777777',
      name: 'Security key',
      createdAt: '2026-09-01T12:00:00Z',
      lastUsedAt: null,
      isBackedUp: false,
    }
    const forbidden = (route: Route) => json(route, { error: 'reauthentication_required' }, 403)

    await page.route(url => url.pathname.startsWith('/api/'), route => json(route, { error: 'not mocked' }, 404))
    await page.route('**/api/account/me', route => json(route, { ...MOCK_USER, twoFactorEnabled: state.twoFactorEnabled }))
    await page.route('**/api/account/security', route => json(route, {
      twoFactorAvailable: true,
      twoFactorEnabled: state.twoFactorEnabled,
      totpConfigured: false,
      hasPassword: true,
      recoveryCodesRemaining: state.twoFactorEnabled ? 10 : 0,
      passkeysAvailable: true,
    }))
    await page.route('**/api/account/passkeys', route => json(route, [passkey]))
    await page.route('**/api/account/passkeys/*', (route) => {
      if (!state.reauthenticated) {
        return forbidden(route)
      }
      return json(route, { error: 'last_second_factor' }, 409)
    })
    await page.route('**/api/account/reauthenticate/passkey-options', (route) => {
      state.challenge = randomBytes(16).toString('base64url')
      return json(route, {
        challenge: state.challenge,
        rpId: 'localhost',
        allowCredentials: [{ type: 'public-key', id: credentialId.toString('base64url') }],
        userVerification: 'preferred',
        timeout: 60000,
      })
    })
    await page.route('**/api/account/reauthenticate', (route) => {
      const body = route.request().postDataJSON() as { password?: string, code?: string, passkeyAssertion?: Assertion }
      expect(body.password).toBe('Password123!')
      if (body.passkeyAssertion) {
        expect(clientData(body.passkeyAssertion.response.clientDataJSON)).toMatchObject({
          type: 'webauthn.get',
          challenge: state.challenge,
          origin,
        })
        expect(flags(body.passkeyAssertion)).toEqual({ present: true, verified: false })
        expect(body.code).toBeUndefined()
        state.reauthentications.push('password+passkey')
      }
      else {
        state.reauthentications.push('password')
      }
      state.reauthenticated = true
      return route.fulfill({ status: 204 })
    })
    await page.route('**/api/account/two-factor/enable', (route) => {
      if (!state.reauthenticated) {
        return forbidden(route)
      }
      state.twoFactorEnabled = true
      // Turning 2FA on issues a new session, which has to confirm itself again.
      state.reauthenticated = false
      return json(route, { twoFactorEnabled: true, recoveryCodes })
    })

    await page.goto('/settings/security')
    const card = page.getByTestId('two-factor-status-card')
    await card.getByTestId('two-factor-enable-passkeys').click()
    const dialog = page.getByTestId('reauthenticate-form')
    await dialog.locator('input[type="password"]').fill('Password123!')
    await dialog.getByRole('button', { name: 'Confirm' }).click()

    await expect(page.getByText(recoveryCodes[0]!)).toBeVisible()
    await expect(page.getByText('Two-factor authentication is on. Your other devices were signed out.')).toBeVisible()

    // With passkey-only 2FA the dialog asks for the password and the passkey, not for a code.
    await page.getByTestId('passkeys-card').getByTestId('passkey-remove').click()
    await page.getByTestId('passkey-remove-confirm').click()
    await expect(dialog).toBeVisible()
    await expect(dialog.getByLabel('Authentication code')).toHaveCount(0)
    await expect(dialog.getByText('Enter your password, then confirm with your passkey.', { exact: false })).toBeVisible()
    await dialog.locator('input[type="password"]').fill('Password123!')
    await dialog.getByTestId('reauth-use-passkey').click()

    await expect(page.getByText('This passkey is your only second factor.', { exact: false })).toBeVisible()
    await expect(page.getByTestId('passkey-name')).toHaveText(['Security key'])
    expect(state.reauthentications).toEqual(['password', 'password+passkey'])
  })
})
