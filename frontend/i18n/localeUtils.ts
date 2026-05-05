export const SUPPORTED_TEXT_LOCALES = ['de'] as const;
export type TextLocale = (typeof SUPPORTED_TEXT_LOCALES)[number];

export const TEXT_TO_FORMAT_LOCALE: Record<TextLocale, string> = {
  de: 'de-AT',
};

export const DEFAULT_TEXT_LOCALE: TextLocale = 'de';
export const DEFAULT_FORMAT_LOCALE = TEXT_TO_FORMAT_LOCALE[DEFAULT_TEXT_LOCALE];

/** POS UI is fixed to German; incoming locale strings are ignored. */
export function normalizeTextLocale(_input: string | null | undefined): TextLocale {
  return DEFAULT_TEXT_LOCALE;
}

/** POS number/date formatting follows German (de-AT) only. */
export function normalizeFormatLocale(_input: string | null | undefined): string {
  return DEFAULT_FORMAT_LOCALE;
}

export function getFormattingLocaleForTextLocale(input: string | null | undefined): string {
  return TEXT_TO_FORMAT_LOCALE[normalizeTextLocale(input)];
}
