/**
 * 403 Forbidden – reasonCode / requiredPolicy → kullanıcı mesajı.
 * Metinler i18n errors.json (errors.forbidden.REASON_CODE) ile yönetilir; axios interceptor getStoredLanguage kullanır.
 * API response message alanı 403 toast için kullanılmaz (dil tutarlılığı).
 */
import { DEFAULT_TEXT_LOCALE, type TextLocale, getCatalog } from '@/i18n/config';
import { getStoredLanguage } from '@/i18n/languageStorage';

export type ForbiddenReasonCode =
  | 'FORBIDDEN'
  | 'USERS_VIEW_REQUIRED'
  | 'USERS_MANAGE_REQUIRED'
  | 'USERS_EXPORT_REQUIRED'
  | 'USERS_ASSIGN_ROLE_REQUIRED'
  | 'USERS_TRANSFER_BRANCH_REQUIRED'
  | 'SCOPE_BRANCH'
  | string;

type ForbiddenMap = Record<string, string>;

/** Katalog yüklenemezse önceki sabit DE metinlerle aynı (davranış korunur). */
const LEGACY_DE_FALLBACK: ForbiddenMap = {
  FORBIDDEN: 'Sie haben keine Berechtigung für diese Aktion.',
  USERS_VIEW_REQUIRED: 'Sie haben keine Berechtigung, Benutzer anzuzeigen.',
  USERS_MANAGE_REQUIRED: 'Sie haben keine Berechtigung, Benutzer zu verwalten.',
  USERS_EXPORT_REQUIRED: 'Sie haben keine Berechtigung, Benutzer zu exportieren.',
  USERS_ASSIGN_ROLE_REQUIRED: 'Sie haben keine Berechtigung, Rollen zuzuweisen.',
  USERS_TRANSFER_BRANCH_REQUIRED: 'Sie haben keine Berechtigung, Benutzer zu versetzen.',
  SCOPE_BRANCH: 'Diese Aktion ist nur innerhalb Ihres Standorts erlaubt.',
};

function readForbiddenMap(locale: TextLocale): ForbiddenMap {
  const raw = getCatalog(locale).errors as { forbidden?: ForbiddenMap } | undefined;
  const fb = raw?.forbidden;
  return fb && typeof fb === 'object' ? fb : {};
}

function normalizeReasonCode(
  reasonCode: ForbiddenReasonCode | null | undefined,
  canonical: ForbiddenMap
): string {
  const c = (reasonCode ?? '').trim();
  const merged: ForbiddenMap = { ...LEGACY_DE_FALLBACK, ...canonical };
  if (c && merged[c]) return c;
  return 'FORBIDDEN';
}

/**
 * @param reasonCode – backend `response.data.reasonCode` veya eşdeğer
 * @param locale – belirtilmezse `getStoredLanguage()` (axios / interceptor uyumu)
 */
export function getForbiddenMessage(
  reasonCode?: ForbiddenReasonCode | null,
  locale?: TextLocale
): string {
  const loc = locale ?? getStoredLanguage();
  const canonical = readForbiddenMap(DEFAULT_TEXT_LOCALE);
  const code = normalizeReasonCode(reasonCode, canonical);

  const primary = readForbiddenMap(loc);
  const fromPrimary = primary[code];
  if (fromPrimary) return fromPrimary;

  const fromDeCatalog = canonical[code];
  if (fromDeCatalog) return fromDeCatalog;

  const legacy = LEGACY_DE_FALLBACK[code];
  if (legacy) return legacy;

  return LEGACY_DE_FALLBACK.FORBIDDEN;
}

/** Map backend requiredPolicy (e.g. UsersView, UsersManage) to reason code for i18n. */
export function mapRequiredPolicyToReasonCode(requiredPolicy?: string | null): string | null {
  if (!requiredPolicy) return null;
  const map: Record<string, string> = {
    UsersView: 'USERS_VIEW_REQUIRED',
    UsersManage: 'USERS_MANAGE_REQUIRED',
    AdminUsers: 'USERS_MANAGE_REQUIRED',
  };
  return map[requiredPolicy] ?? null;
}
