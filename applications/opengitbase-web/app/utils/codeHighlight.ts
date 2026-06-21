import { codeToHtml } from 'shiki'
import { languageFromPath } from '~/utils/codeLanguage'

type HighlightTheme = 'github-light' | 'github-dark'

function themeForColorMode(colorMode: string): HighlightTheme {
  return colorMode === 'dark' ? 'github-dark' : 'github-light'
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
  return `<pre class="shiki"><code>${escapeHtml(source)}</code></pre>`
}

export async function highlightSourceCode(
  source: string,
  path: string,
  colorMode: string,
): Promise<string> {
  const lang = languageFromPath(path)
  const theme = themeForColorMode(colorMode)

  try {
    return await codeToHtml(source, { lang, theme })
  }
  catch {
    try {
      return await codeToHtml(source, { lang: 'text', theme })
    }
    catch {
      return plainCodeHtml(source)
    }
  }
}
