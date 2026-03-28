/**
 * Gelecekte backend `code` alanları için i18n anahtarı eşlemesi (bootstrap veya modül yükünde kayıt).
 * Generic API katmanı formal report / operator copy ile karıştırılmaz.
 */

const codeToI18nKey = new Map<string, string>();

function normalizeCodeKey(code: string): string {
  return code.trim().toUpperCase();
}

/** Kalıcı eşleme: örn. registerApiErrorCodeTranslation('INVOICE_LOCKED', 'invoices.errors.locked') */
export function registerApiErrorCodeTranslation(code: string, messageKey: string): void {
  codeToI18nKey.set(normalizeCodeKey(code), messageKey);
}

/** Vitest izolasyonu */
export function clearApiErrorCodeRegistryForTests(): void {
  codeToI18nKey.clear();
}

export function getRegisteredMessageKeyForApiErrorCode(code: string | undefined): string | undefined {
  if (!code?.trim()) return undefined;
  const u = normalizeCodeKey(code);
  return codeToI18nKey.get(u) ?? codeToI18nKey.get(code.trim());
}
