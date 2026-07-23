/**
 * Pure i18n-aware date formatting (Day.js patterns from `common.dateFormat.*`).
 * Used by {@link useFormattedDate} / {@link DateColumn}; safe to call outside React.
 */
import { USER_FACING_MISSING_TRANSLATION_LABEL } from '@/i18n/translationFallback';
import dayjs, {
  type DateInput,
  type DateLocale,
  EMPTY_DATE_DISPLAY,
} from '@/lib/dateUtils';

export type DateFormatKey =
  | 'short'
  | 'medium'
  | 'long'
  | 'datetime'
  | 'datetimeSeconds'
  | 'weekdayShort'
  | 'weekdayLong'
  | 'monthLong'
  | 'monthShort';

/** Fallback Day.js patterns when a catalog key is missing. */
export const DATE_FORMAT_FALLBACKS: Record<DateFormatKey, string> = {
  short: 'DD.MM.YYYY',
  medium: 'DD. MMMM YYYY',
  long: 'DD. MMMM YYYY HH:mm',
  datetime: 'DD.MM.YYYY HH:mm',
  datetimeSeconds: 'DD.MM.YYYY HH:mm:ss',
  weekdayShort: 'dd',
  weekdayLong: 'dddd',
  monthLong: 'MMMM',
  monthShort: 'MMM',
};

export type FormatLocalizedDateOptions = {
  /** Parse/format in UTC (API timestamps stamped with Z). */
  utc?: boolean;
};

function looksLikeDayjsFormat(value: string): boolean {
  return /[YyMdHhSsAaZz]/.test(value);
}

export function resolveDateFormatString(
  translate: (key: string) => string,
  formatKey: DateFormatKey
): string {
  const fromCatalog = translate(`common.dateFormat.${formatKey}`);
  if (
    !fromCatalog ||
    fromCatalog === USER_FACING_MISSING_TRANSLATION_LABEL ||
    !looksLikeDayjsFormat(fromCatalog)
  ) {
    return DATE_FORMAT_FALLBACKS[formatKey];
  }
  return fromCatalog;
}

export function formatLocalizedDate(
  date: DateInput,
  formatKey: DateFormatKey,
  textLocale: string,
  translate: (key: string) => string,
  options?: FormatLocalizedDateOptions
): string {
  if (date == null || date === '') return EMPTY_DATE_DISPLAY;
  const parsed = options?.utc ? dayjs.utc(date) : dayjs(date);
  if (!parsed.isValid()) return EMPTY_DATE_DISPLAY;
  const pattern = resolveDateFormatString(translate, formatKey);
  return parsed.locale(textLocale as DateLocale).format(pattern);
}
