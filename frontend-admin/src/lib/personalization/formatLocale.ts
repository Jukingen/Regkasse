import { type TextLocale, getFormattingLocaleForTextLocale } from '@/i18n/config';

import type { DateFormatPattern } from './types';

export function resolveFormatLocaleForDateFormat(
  dateFormat: DateFormatPattern,
  textLocale: TextLocale
): string {
  switch (dateFormat) {
    case 'DD.MM.YYYY':
      return 'de-AT';
    case 'MM/DD/YYYY':
      return 'en-US';
    case 'YYYY-MM-DD':
      return 'sv-SE';
    default:
      return getFormattingLocaleForTextLocale(textLocale);
  }
}
