export const SUPPORTED_TEXT_LOCALES = ['de', 'en', 'tr'] as const;
export type TextLocale = (typeof SUPPORTED_TEXT_LOCALES)[number];

export const TEXT_TO_FORMAT_LOCALE: Record<TextLocale, string> = {
  de: 'de-AT',
  en: 'en-US',
  tr: 'tr-TR',
};

export const DEFAULT_TEXT_LOCALE: TextLocale = 'de';
export const DEFAULT_FORMAT_LOCALE = TEXT_TO_FORMAT_LOCALE[DEFAULT_TEXT_LOCALE];

export function normalizeTextLocale(input: string | null | undefined): TextLocale {
  if (!input) return DEFAULT_TEXT_LOCALE;
  const normalized = input.toLowerCase().replace('_', '-');
  if ((SUPPORTED_TEXT_LOCALES as readonly string[]).includes(normalized)) {
    return normalized as TextLocale;
  }
  if (normalized.startsWith('de')) return 'de';
  if (normalized.startsWith('en')) return 'en';
  if (normalized.startsWith('tr')) return 'tr';
  return DEFAULT_TEXT_LOCALE;
}

export function normalizeFormatLocale(input: string | null | undefined): string {
  if (!input) return DEFAULT_FORMAT_LOCALE;
  const normalized = input.toLowerCase().replace('_', '-');
  if (normalized === 'de' || normalized.startsWith('de-')) return 'de-AT';
  if (normalized === 'en' || normalized.startsWith('en-')) return 'en-US';
  if (normalized === 'tr' || normalized.startsWith('tr-')) return 'tr-TR';
  return DEFAULT_FORMAT_LOCALE;
}

export function getFormattingLocaleForTextLocale(input: string | null | undefined): string {
  const textLocale = normalizeTextLocale(input);
  return TEXT_TO_FORMAT_LOCALE[textLocale];
}
