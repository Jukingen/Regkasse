export const SUPPORTED_TEXT_LOCALES = ['de', 'en', 'tr'] as const;
export type TextLocale = (typeof SUPPORTED_TEXT_LOCALES)[number];

export const TEXT_TO_FORMAT_LOCALE: Record<TextLocale, string> = {
  de: 'de-AT',
  en: 'en-US',
  tr: 'tr-TR',
};

export const DEFAULT_TEXT_LOCALE: TextLocale = 'de';
export const DEFAULT_FORMAT_LOCALE = TEXT_TO_FORMAT_LOCALE[DEFAULT_TEXT_LOCALE];

/** Minimal shape from `expo-localization` `getLocales()` / `useLocales()`. */
export type DeviceLocaleLike = {
  languageCode?: string | null;
  languageTag?: string | null;
};

/**
 * Maps API / BCP-47 tags (e.g. de-DE) to a supported POS text locale, or null if unsupported.
 * Use this when walking device preference lists so unsupported primary locales
 * do not force German before a later supported preference is checked.
 */
export function matchSupportedTextLocale(input: string | null | undefined): TextLocale | null {
  if (!input) return null;
  const normalized = input.toLowerCase().replaceAll('_', '-');
  if ((SUPPORTED_TEXT_LOCALES as readonly string[]).includes(normalized)) {
    return normalized as TextLocale;
  }
  if (normalized.startsWith('de')) return 'de';
  if (normalized.startsWith('en')) return 'en';
  if (normalized.startsWith('tr')) return 'tr';
  return null;
}

/** Maps API / BCP-47 tags (e.g. de-DE) to POS text locales (de | en | tr). */
export function normalizeTextLocale(input: string | null | undefined): TextLocale {
  return matchSupportedTextLocale(input) ?? DEFAULT_TEXT_LOCALE;
}

/**
 * Picks the first supported language from the device preferred-locale list
 * (order matches OS settings). Falls back to German when none match.
 */
export function resolveTextLocaleFromDeviceLocales(
  locales: readonly DeviceLocaleLike[] | null | undefined
): TextLocale {
  if (!locales?.length) return DEFAULT_TEXT_LOCALE;
  for (const locale of locales) {
    const matched =
      matchSupportedTextLocale(locale.languageCode) ?? matchSupportedTextLocale(locale.languageTag);
    if (matched) return matched;
  }
  return DEFAULT_TEXT_LOCALE;
}

/**
 * Initial POS UI language: saved preference → device locales → German.
 */
export function resolveInitialTextLocale(options: {
  savedLanguage?: string | null;
  deviceLocales?: readonly DeviceLocaleLike[] | null;
}): TextLocale {
  const fromSaved = matchSupportedTextLocale(options.savedLanguage);
  if (fromSaved) return fromSaved;
  return resolveTextLocaleFromDeviceLocales(options.deviceLocales);
}

/** Maps free-form locale tags to Intl formatting locales used by POS. */
export function normalizeFormatLocale(input: string | null | undefined): string {
  if (!input) return DEFAULT_FORMAT_LOCALE;
  const normalized = input.toLowerCase().replaceAll('_', '-');
  if (normalized === 'de' || normalized.startsWith('de-')) return 'de-AT';
  if (normalized === 'en' || normalized.startsWith('en-')) return 'en-US';
  if (normalized === 'tr' || normalized.startsWith('tr-')) return 'tr-TR';
  return DEFAULT_FORMAT_LOCALE;
}

export function getFormattingLocaleForTextLocale(input: string | null | undefined): string {
  const textLocale = normalizeTextLocale(input);
  return TEXT_TO_FORMAT_LOCALE[textLocale];
}

/** Maps POS text locale to UserSettings API language codes. */
export function toUserSettingsLanguage(locale: TextLocale): 'de-DE' | 'en' | 'tr' {
  if (locale === 'en') return 'en';
  if (locale === 'tr') return 'tr';
  return 'de-DE';
}
