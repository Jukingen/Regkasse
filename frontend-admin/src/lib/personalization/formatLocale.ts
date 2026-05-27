import { getFormattingLocaleForTextLocale, type TextLocale } from '@/i18n/config';
import type { DateFormatPattern } from './types';

export function resolveFormatLocaleForDateFormat(
  dateFormat: DateFormatPattern,
  textLocale: TextLocale,
): string {
  switch (dateFormat) {
    case 'DD.MM.YYYY':
      return 'de-AT';
    case 'YYYY-MM-DD':
      return 'en-GB';
    case 'MM/DD/YYYY':
      return 'en-US';
    default:
      return getFormattingLocaleForTextLocale(textLocale);
  }
}
