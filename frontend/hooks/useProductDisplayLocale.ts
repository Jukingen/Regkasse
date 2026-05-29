import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';

import {
  normalizeProductTextLocale,
  type ProductTextLocale,
} from '../utils/productLocalization';

/** Current POS language for product name/description resolution (de | en | tr). */
export function useProductDisplayLocale(): ProductTextLocale {
  const { i18n } = useTranslation();
  return useMemo(
    () => normalizeProductTextLocale(i18n.resolvedLanguage ?? i18n.language),
    [i18n.language, i18n.resolvedLanguage],
  );
}
