import { generateKeyPairSync, randomBytes } from 'node:crypto'
import type { CDPSession, Page, Route } from '@playwright/test'
import { expect, test } from '@playwright/test'
import { PAGE_READY_TIMEOUT, blockServiceWorker, waitForPageReady } from './helpers'

const MOCK_USER = {
  userId: '22222222-2222-2222-2222-222222222222',
  username: 'demo-user',
  emailVerified: true,
  isAdmin: false,
  twoFactorEnabled: false,
}

const USER_HANDLE = Buffer.alloc(16, 0x22)

function json(route: Route, body: unknown, status = 200) {
  return route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) })
}

function clientData(encoded: string): { type: string, challenge: string, origin: string } {
  return JSON.parse(Buffer.from(encoded, 'base64url').toString('utf8'))
}

/**
 * A stand-in for the sign-in API that checks what Chromium's authenticator sends: the challenge of
 * the latest options, the page origin, the ceremony type, the stored credential and its user handle.
 */
async function mockSignInApi(page: Page, origin: string, credentialId: Buffer) {
  const state = { signedIn: false, challenges: [] as string[], optionsRequests: 0, passwordLogins: 0 }

  await page.route(url => url.pathname.startsWith('/api/'), route => json(route, { error: 'not mocked' }, 404))
  await page.route('**/api/account/me', route =>
    state.signedIn ? json(route, MOCK_USER) : json(route, { error: 'unauthorized' }, 401))
  await page.route('**/api/signin/login', (route) => {
    state.passwordLogins++
    return json(route, { error: 'invalid' }, 401)
  })
  await page.route('**/api/signin/passkey/options', (route) => {
    state.optionsRequests++
    const challenge = randomBytes(16).toString('base64url')
    state.challenges.push(challenge)
    return json(route, { challenge, rpId: 'localhost', allowCredentials: [], userVerification: 'required', timeout: 60000 })
  })
  await page.route('**/api/signin/passkey', async (route) => {
    const body = route.request().postDataJSON() as {
      assertion: { id: string, type: string, response: { clientDataJSON: string, userHandle?: string, signature: string } }
    }
    const data = clientData(body.assertion.response.clientDataJSON)
    expect(data.type).toBe('webauthn.get')
    expect(data.origin).toBe(origin)
    expect(state.challenges).toContain(data.challenge)
    expect(body.assertion.id).toBe(credentialId.toString('base64url'))
    expect(body.assertion.response.userHandle).toBe(USER_HANDLE.toString('base64url'))
    expect(body.assertion.response.signature).toBeTruthy()
    state.signedIn = true
    return route.fulfill({ status: 200, contentType: 'text/plain', body: 'session-token' })
  })
  return state
}

/** A virtual platform authenticator holding one discoverable passkey for `localhost`. */
async function addPasskey(cdp: CDPSession, credentialId: Buffer) {
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
  const { privateKey } = generateKeyPairSync('ec', { namedCurve: 'P-256' })
  await cdp.send('WebAuthn.addCredential', {
    authenticatorId,
    credential: {
      credentialId: credentialId.toString('base64'),
      isResidentCredential: true,
      rpId: 'localhost',
      privateKey: privateKey.export({ format: 'der', type: 'pkcs8' }).toString('base64'),
      userHandle: USER_HANDLE.toString('base64'),
      signCount: 0,
    },
  })
  return authenticatorId
}

/** Without conditional mediation the page offers only the button, which the test then clicks. */
async function withoutAutofill(page: Page) {
  await page.addInitScript(() => {
    Object.defineProperty(PublicKeyCredential, 'isConditionalMediationAvailable', { value: async () => false })
  })
}

async function setUp(page: Page, baseURL: string | undefined) {
  await blockServiceWorker(page)
  const credentialId = randomBytes(16)
  const api = await mockSignInApi(page, new URL(baseURL!).origin, credentialId)
  const cdp = await page.context().newCDPSession(page)
  const authenticatorId = await addPasskey(cdp, credentialId)
  return { api, cdp, authenticatorId }
}

test.describe('passkey sign-in', () => {
  test.beforeEach(({ page: _page }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop', 'One WebAuthn run is enough; the layout is covered by snapshots.')
  })

  test('the button signs in with the passkey', async ({ page, baseURL }) => {
    await withoutAutofill(page)
    const { api, cdp, authenticatorId } = await setUp(page, baseURL)

    await page.goto('/sign-in?redirect=/settings')
    await page.getByTestId('passkey-sign-in').click()

    await expect.poll(() => new URL(page.url()).pathname, { timeout: PAGE_READY_TIMEOUT }).toBe('/settings')
    expect(api.signedIn).toBe(true)
    expect(api.passwordLogins).toBe(0)
    const { credentials } = await cdp.send('WebAuthn.getCredentials', { authenticatorId })
    expect(credentials[0]?.signCount).toBe(1)
  })

  test('the username field offers the passkey', async ({ page, baseURL }) => {
    const { api } = await setUp(page, baseURL)

    // Chromium answers a conditional request from its virtual authenticator at once, standing in for
    // the user picking the passkey from the autofill menu. Nothing is clicked.
    await page.goto('/sign-in?redirect=/settings')

    await expect.poll(() => new URL(page.url()).pathname, { timeout: PAGE_READY_TIMEOUT }).toBe('/settings')
    expect(api.signedIn).toBe(true)
    expect(api.optionsRequests).toBe(1)
    expect(api.passwordLogins).toBe(0)
  })

  test('the CLI page hands the passkey session to the CLI', async ({ page, baseURL }) => {
    await withoutAutofill(page)
    const { api } = await setUp(page, baseURL)
    const callback = page.waitForRequest(request => new URL(request.url()).hostname === '127.0.0.1')
    await page.route('http://127.0.0.1:54321/**', route => route.fulfill({ status: 200, contentType: 'text/plain', body: 'done' }))

    await page.goto('/cli/auth?port=54321&state=cli-state')
    await page.getByTestId('passkey-sign-in').click()

    const url = new URL((await callback).url())
    expect(url.pathname).toBe('/callback')
    expect(url.searchParams.get('token')).toBe('session-token')
    expect(url.searchParams.get('state')).toBe('cli-state')
    expect(api.signedIn).toBe(true)
  })

  test('hides passkeys when the API has them switched off', async ({ page }) => {
    await blockServiceWorker(page)
    await page.route(url => url.pathname.startsWith('/api/'), route => json(route, { error: 'not found' }, 404))
    await page.route('**/api/account/me', route => json(route, { error: 'unauthorized' }, 401))

    await page.goto('/sign-in')
    await expect(page.getByRole('button', { name: 'Sign in', exact: true })).toBeVisible()
    await waitForPageReady(page)

    await expect(page.getByTestId('passkey-sign-in')).toHaveCount(0)
    await expect(page.getByRole('textbox').first()).toHaveAttribute('autocomplete', 'username webauthn')
  })

  test('a password sign-in cancels the passkey autofill', async ({ page, baseURL }) => {
    await blockServiceWorker(page)
    const api = await mockSignInApi(page, new URL(baseURL!).origin, randomBytes(16))
    // The user never picks a passkey: the autofill request stays open until the page cancels it.
    await page.addInitScript(() => {
      const calls: string[] = []
      Object.assign(window, { passkeyRequests: calls })
      navigator.credentials.get = (options?: CredentialRequestOptions) => {
        calls.push(options?.mediation ?? 'modal')
        return new Promise((_resolve, reject) => {
          options?.signal?.addEventListener('abort', () => {
            calls.push('aborted')
            reject(new DOMException('The request was cancelled.', 'AbortError'))
          })
        })
      }
    })

    await page.goto('/sign-in')
    await expect.poll(
      () => page.evaluate(() => (window as unknown as { passkeyRequests: string[] }).passkeyRequests),
      { timeout: PAGE_READY_TIMEOUT },
    ).toEqual(['conditional'])
    await page.getByRole('textbox').first().fill('demo-user')
    await page.locator('input[type="password"]').fill('wrong')
    await page.getByRole('button', { name: 'Sign in', exact: true }).click()

    await expect(page.getByText('Invalid username or password.')).toBeVisible()
    // Cancelled for the password attempt, then offered again after it failed.
    await expect.poll(() => page.evaluate(() => (window as unknown as { passkeyRequests: string[] }).passkeyRequests))
      .toEqual(['conditional', 'aborted', 'conditional'])
    expect(api.passwordLogins).toBe(1)
    expect(api.signedIn).toBe(false)
  })
})
