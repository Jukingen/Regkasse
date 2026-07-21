export {
  ADMIN_REGISTERED_NAMESPACES,
  DEFAULT_TEXT_LOCALE,
  getFormattingLocaleForTextLocale,
  normalizeFormatLocale,
  normalizeTextLocale,
  SUPPORTED_TEXT_LOCALES,
  type TextLocale,
} from './config';
export {
  createIntlFormatters,
  FORMAT_EMPTY_DISPLAY,
  formatCurrency,
  formatDate,
  formatDateTime,
  formatNumber,
  formatPercent,
} from './formatting';
export { I18nProvider, useI18n } from './I18nProvider';
export {
  getNavigatorTextLocaleSuggestion,
  getStoredLanguage,
  setStoredLanguage,
} from './languageStorage';
export { USER_FACING_MISSING_TRANSLATION_LABEL } from './translationFallback';
export {
  DAYJS_DATE_FORMAT,
  DAYJS_DATETIME_FORMAT,
  DAYJS_DATETIME_SECONDS_FORMAT,
  formatGermanDate,
  formatGermanDateTime,
  formatGermanTime,
  formatUserDate,
  formatUserDateTime,
  formatUserMonthDay,
  formatUserMonthYear,
  GERMAN_DATE_EMPTY,
  toDayjsDateFormat,
} from '@/lib/dateFormatter';
export { useDateFormatter } from '@/lib/hooks/useDateFormatter';
