const EXTENSION_TO_LANGUAGE: Record<string, string> = {
  astro: 'astro',
  bash: 'bash',
  bat: 'bat',
  c: 'c',
  cc: 'cpp',
  cfg: 'ini',
  clj: 'clojure',
  cljs: 'clojure',
  cmd: 'bat',
  conf: 'ini',
  cpp: 'cpp',
  cs: 'csharp',
  css: 'css',
  cts: 'typescript',
  cxx: 'cpp',
  dart: 'dart',
  dockerfile: 'dockerfile',
  ex: 'elixir',
  exs: 'elixir',
  fs: 'fsharp',
  fsx: 'fsharp',
  gql: 'graphql',
  graphql: 'graphql',
  go: 'go',
  groovy: 'groovy',
  h: 'cpp',
  hcl: 'hcl',
  hs: 'haskell',
  hpp: 'cpp',
  htm: 'html',
  html: 'html',
  hxx: 'cpp',
  ini: 'ini',
  java: 'java',
  js: 'javascript',
  json: 'json',
  jsonc: 'jsonc',
  jsx: 'jsx',
  kt: 'kotlin',
  kts: 'kotlin',
  less: 'less',
  lua: 'lua',
  mjs: 'javascript',
  mts: 'typescript',
  md: 'markdown',
  markdown: 'markdown',
  php: 'php',
  pl: 'perl',
  proto: 'protobuf',
  ps1: 'powershell',
  pwsh: 'powershell',
  py: 'python',
  pyw: 'python',
  r: 'r',
  rb: 'ruby',
  rs: 'rust',
  sass: 'sass',
  scala: 'scala',
  scss: 'scss',
  sh: 'bash',
  sql: 'sql',
  svelte: 'svelte',
  swift: 'swift',
  tf: 'hcl',
  toml: 'toml',
  ts: 'typescript',
  tsx: 'tsx',
  vb: 'vb',
  vue: 'vue',
  xml: 'xml',
  yaml: 'yaml',
  yml: 'yaml',
  zig: 'zig',
  zsh: 'bash',
}

const SPECIAL_FILENAMES: Record<string, string> = {
  dockerfile: 'dockerfile',
  makefile: 'makefile',
  'cmakelists.txt': 'cmake',
}

export function languageFromPath(path: string): string {
  const fileName = path.split('/').pop()?.toLowerCase() ?? ''
  if (!fileName) {
    return 'text'
  }

  const specialLanguage = SPECIAL_FILENAMES[fileName]
  if (specialLanguage) {
    return specialLanguage
  }

  const dotIndex = fileName.lastIndexOf('.')
  if (dotIndex <= 0) {
    return 'text'
  }

  const extension = fileName.slice(dotIndex + 1)
  return EXTENSION_TO_LANGUAGE[extension] ?? 'text'
}

const FENCE_LANGUAGE_ALIASES: Record<string, string> = {
  cjs: 'javascript',
  cs: 'csharp',
  'c#': 'csharp',
  h: 'cpp',
  hpp: 'cpp',
  js: 'javascript',
  md: 'markdown',
  mjs: 'javascript',
  mts: 'typescript',
  py: 'python',
  rb: 'ruby',
  rs: 'rust',
  sh: 'bash',
  ts: 'typescript',
  yml: 'yaml',
  zsh: 'bash',
}

/** Map markdown fence info strings to shiki language ids. */
export function languageFromFenceTag(tag: string): string {
  const normalized = tag.trim().toLowerCase()
  if (!normalized) {
    return 'text'
  }
  return FENCE_LANGUAGE_ALIASES[normalized] ?? normalized
}
