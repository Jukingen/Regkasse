/**
 * Türkçe: `AdminTranslationKey` union (generated) ve kademeli tip güvenliği için ince yardımcılar.
 * Dinamik segmentler için whitelist/map veya `string` + runtime `i18n:ci` doğrulaması kullanın.
 */
import type { AdminTranslationKey } from './generated/translationKeys';
import { ADMIN_TRANSLATION_KEY_SET } from './generated/translationKeys';

export type { AdminTranslationKey } from './generated/translationKeys';
export { ADMIN_TRANSLATION_KEY_SET, ADMIN_TRANSLATION_KEYS } from './generated/translationKeys';

export function isAdminTranslationKey(key: string): key is AdminTranslationKey {
  return ADMIN_TRANSLATION_KEY_SET.has(key);
}

type TranslateOptions = Record<string, string | number>;

/** Mevcut `t` imzasını korur; yalnızca bilinen anahtarlar için kullanın (kademeli benimseme). */
export function typedT(
  t: (key: string, options?: TranslateOptions) => string,
  key: AdminTranslationKey,
  options?: TranslateOptions
): string {
  return t(key, options);
}
