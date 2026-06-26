export interface RelativeTimeOptions {
  now?: number | Date
  locale?: string
}

const SECOND = 1
const MINUTE = 60
const HOUR = 60 * MINUTE
const DAY = 24 * HOUR
const WEEK = 7 * DAY
const MONTH = 30 * DAY
const YEAR = 365 * DAY

const UNIT_STEPS: Array<{ unit: Intl.RelativeTimeFormatUnit, seconds: number }> = [
  { unit: 'year', seconds: YEAR },
  { unit: 'month', seconds: MONTH },
  { unit: 'week', seconds: WEEK },
  { unit: 'day', seconds: DAY },
  { unit: 'hour', seconds: HOUR },
  { unit: 'minute', seconds: MINUTE },
  { unit: 'second', seconds: SECOND },
]

export function resolveIntlLocale(locale = 'en'): string {
  if (locale.includes('-')) {
    return locale
  }

  const localeMap: Record<string, string> = {
    en: 'en-US',
  }

  return localeMap[locale] ?? locale
}

export function formatRelativeTime(
  iso: string,
  options: RelativeTimeOptions = {},
): string {
  const now = options.now instanceof Date
    ? options.now.getTime()
    : options.now ?? Date.now()
  const then = new Date(iso).getTime()

  if (Number.isNaN(then)) {
    return iso
  }

  const diffSeconds = Math.round((then - now) / 1000)
  const absSeconds = Math.abs(diffSeconds)
  const formatter = new Intl.RelativeTimeFormat(resolveIntlLocale(options.locale), {
    numeric: 'auto',
  })

  for (const step of UNIT_STEPS) {
    if (absSeconds >= step.seconds || step.unit === 'second') {
      const value = Math.round(diffSeconds / step.seconds)
      return formatter.format(value, step.unit)
    }
  }

  return formatter.format(0, 'second')
}

export function formatAbsoluteTime(
  iso: string,
  options: Pick<RelativeTimeOptions, 'locale'> = {},
): string {
  const date = new Date(iso)
  if (Number.isNaN(date.getTime())) {
    return iso
  }

  return new Intl.DateTimeFormat(resolveIntlLocale(options.locale), {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(date)
}
