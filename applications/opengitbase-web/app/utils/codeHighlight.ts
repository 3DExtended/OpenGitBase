import {
  createHighlighter,
  type BundledLanguage,
  type Highlighter,
} from 'shiki'
import { inferCodeLanguage, languageFromPath } from './codeLanguage'

type HighlightTheme = 'github-light' | 'github-dark'

let highlighterPromise: Promise<Highlighter> | null = null

export function themeForColorMode(colorMode: string): HighlightTheme {
  if (colorMode === 'dark') {
    return 'github-dark'
  }
  if (colorMode === 'light') {
    return 'github-light'
  }
  if (import.meta.client && window.matchMedia('(prefers-color-scheme: dark)').matches) {
    return 'github-dark'
  }
  return 'github-light'
}

function escapeHtml(source: string): string {
  return source
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll('\'', '&#39;')
}

function plainCodeHtml(source: string): string {
  const body = source
    .split('\n')
    .map(line => `<span class="line"><span>${escapeHtml(line)}</span></span>`)
    .join('')
  return `<pre class="shiki"><code>${body}</code></pre>`
}

export function resolveHighlightLanguage(
  source: string,
  path: string,
  languageOverride?: string,
): string {
  if (languageOverride && languageOverride !== 'text') {
    return languageOverride
  }

  const inferred = inferCodeLanguage(source)
  if (inferred) {
    return inferred
  }

  const fromPath = languageFromPath(path)
  if (fromPath !== 'text') {
    return fromPath
  }

  return languageOverride ?? 'text'
}

async function getHighlighter(): Promise<Highlighter> {
  if (!highlighterPromise) {
    highlighterPromise = createHighlighter({
      themes: ['github-light', 'github-dark'],
      langs: ['text', 'typescript', 'javascript', 'tsx', 'jsx', 'json'],
    }).catch((error: unknown) => {
      highlighterPromise = null
      throw error
    })
  }
  return highlighterPromise
}

async function resolveLanguage(highlighter: Highlighter, lang: string): Promise<BundledLanguage> {
  const loaded = highlighter.getLoadedLanguages()
  if (loaded.includes(lang as BundledLanguage)) {
    return lang as BundledLanguage
  }
  try {
    await highlighter.loadLanguage(lang as BundledLanguage)
    return lang as BundledLanguage
  }
  catch {
    if (!loaded.includes('text')) {
      await highlighter.loadLanguage('text')
    }
    return 'text'
  }
}

export async function highlightSourceCode(
  source: string,
  path: string,
  colorMode: string,
  languageOverride?: string,
): Promise<string> {
  const lang = resolveHighlightLanguage(source, path, languageOverride)
  const theme = themeForColorMode(colorMode)

  try {
    const highlighter = await getHighlighter()
    const langToUse = await resolveLanguage(highlighter, lang)
    return highlighter.codeToHtml(source, { lang: langToUse, theme })
  }
  catch {
    return plainCodeHtml(source)
  }
}
