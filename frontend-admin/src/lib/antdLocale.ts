import type { Locale } from 'antd/es/locale';
import deDE from 'antd/locale/de_DE';
import enUS from 'antd/locale/en_US';
import trTR from 'antd/locale/tr_TR';

/** Ant Design UI locale packages for DatePicker, Calendar, Table pagination, etc. */
const LOCALE_MAP: Record<string, Locale> = {
  de: deDE,
  'de-DE': deDE,
  'de-AT': deDE,
  en: enUS,
  'en-US': enUS,
  tr: trTR,
  'tr-TR': trTR,
};

const DEFAULT_ANTD_LOCALE: Locale = deDE;

/**
 * Resolve Ant Design ConfigProvider locale from Admin text/format locale.
 * Falls back to German (`de_DE`) — Admin default language.
 */
export function getAntdLocale(locale: string | null | undefined): Locale {
  if (!locale) return DEFAULT_ANTD_LOCALE;
  const direct = LOCALE_MAP[locale];
  if (direct) return direct;

  const normalized = locale.trim().toLowerCase();
  if (normalized.startsWith('tr')) return trTR;
  if (normalized.startsWith('en')) return enUS;
  if (normalized.startsWith('de')) return deDE;
  return DEFAULT_ANTD_LOCALE;
}

export { deDE, enUS, trTR };
