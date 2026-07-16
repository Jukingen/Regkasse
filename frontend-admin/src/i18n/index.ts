export { I18nProvider, useI18n } from './I18nProvider';
export {
  ADMIN_REGISTERED_NAMESPACES,
  DEFAULT_TEXT_LOCALE,
  SUPPORTED_TEXT_LOCALES,
  getFormattingLocaleForTextLocale,
  normalizeTextLocale,
  normalizeFormatLocale,
  type TextLocale,
} from './config';
export { getNavigatorTextLocaleSuggestion, getStoredLanguage, setStoredLanguage } from './languageStorage';
export { USER_FACING_MISSING_TRANSLATION_LABEL } from './translationFallback';
export {
  FORMAT_EMPTY_DISPLAY,
  createIntlFormatters,
  formatCurrency,
  formatDate,
  formatDateTime,
  formatNumber,
  formatPercent,
} from './formatting';
export {
  DAYJS_DATE_FORMAT,
  DAYJS_DATETIME_FORMAT,
  DAYJS_DATETIME_SECONDS_FORMAT,
  GERMAN_DATE_EMPTY,
  formatGermanDate,
  formatGermanDateTime,
  formatGermanTime,
  formatUserDate,
  formatUserDateTime,
  formatUserMonthDay,
  formatUserMonthYear,
  toDayjsDateFormat,
} from '@/lib/dateFormatter';
export { useDateFormatter } from '@/lib/hooks/useDateFormatter';
