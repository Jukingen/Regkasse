import { getFormattingLocaleForTextLocale, type TextLocale } from '@/i18n/config';
import type { DateFormatPattern } from './types';

export function resolveFormatLocaleForDateFormat(
  dateFormat: DateFormatPattern,
  textLocale: TextLocale,
): string {
  if (dateFormat === 'DD.MM.YYYY') {
    return 'de-AT';
  }
  return getFormattingLocaleForTextLocale(textLocale);
}
