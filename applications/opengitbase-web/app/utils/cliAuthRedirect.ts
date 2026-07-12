export interface CliAuthQueryParams {
  port: string
  state: string
}

export function parseCliAuthQueryParams(
  port: string | string[] | undefined,
  state: string | string[] | undefined,
): CliAuthQueryParams | null {
  const portValue = typeof port === 'string' ? port : undefined
  const stateValue = typeof state === 'string' ? state : undefined

  if (!portValue || !stateValue) {
    return null
  }

  const portNumber = Number.parseInt(portValue, 10)
  if (!Number.isInteger(portNumber) || portNumber <= 0 || portNumber > 65535) {
    return null
  }

  return { port: portValue, state: stateValue }
}

export function buildCliAuthCallbackUrl(port: string, state: string, token: string): string {
  const url = new URL(`http://127.0.0.1:${port}/callback`)
  url.searchParams.set('token', token)
  url.searchParams.set('state', state)
  return url.toString()
}
