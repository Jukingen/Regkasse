import type { TextLocale } from '@/i18n/config';

/**
 * General (non–formal-report) dual-locale pick for operational messages.
 * For formal fiscal / legal export copy, use `resolveFormalReportContentFromDualFields` or
 * `resolveFormalReportLegalExportIssueMessage` from `../reporting/formalReportContentResolver` — Turkish is
 * never used for that layer even when `TextLocale === 'tr'`.
 *
 * - `en`: prefer `messageEn`, then `messageDe`.
 * - `de` / `tr` / other: prefer `messageDe`, then `messageEn`.
 */
export function pickDualLocaleMessage(
  messageDe: string,
  messageEn: string | null | undefined,
  locale: TextLocale
): string {
  const de = (messageDe ?? '').trim();
  const en = (messageEn ?? '').trim();

  if (locale === 'en') {
    if (en.length > 0) return en;
    if (de.length > 0) return de;
    return '';
  }

  if (de.length > 0) return de;
  if (en.length > 0) return en;
  return '';
}

/**
 * Yalnızca Almanca üretilen API alanları (`*De`, `noteDe`, `operatorHintDe`, …).
 * Şimdilik değeri olduğu gibi döndürür; ileride reasonCode / sabit kod → i18n eşlemesi burada toplanacak.
 *
 * Formal reporting UI: use `resolveFormalReportContentFromDualFields` from `../reporting/formalReportContentResolver` instead.
 */
export function pickDeOnlyBackendText(value: string | null | undefined): string | undefined {
  if (value == null) return undefined;
  const s = String(value).trim();
  return s.length > 0 ? s : undefined;
}

/**
 * `remediationHintsDe` gibi dizi alanlarını birleştirir (ayırıcı sayfa bazında değişebilir).
 */
export function joinDeOnlyBackendList(
  items: readonly string[] | null | undefined,
  separator: string
): string | undefined {
  if (!items?.length) return undefined;
  const parts = items.map((x) => String(x).trim()).filter((x) => x.length > 0);
  return parts.length > 0 ? parts.join(separator) : undefined;
}

/**
 * `ReportSubmissionEnvelopeDto.RejectionReasons` gibi makine kodları — şimdilik ham kod.
 * Sonraki adım: `reporting.rejectionReasons.<code>` katalog anahtarları + `t()` ile gösterim.
 */
export function formatRejectionReasonForDisplay(code: string): string {
  return String(code ?? '').trim() || '—';
}
