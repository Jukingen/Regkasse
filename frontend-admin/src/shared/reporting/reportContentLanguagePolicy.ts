import type { TextLocale } from '@/i18n/config';

/**
 * Resmi rapor içeriği için izin verilen diller (`de` | `en`). API’den gelen metin asla Türkçe
 * render edilmez; arayüz dili (`TextLocale`) ayrı kalır.
 *
 * @see Sunucu alanları için çözümleyici: `../backendLocale/fiscalReportTextPolicy` içindeki `resolveFiscalReportBackendText`
 */
export const FORMAL_REPORT_ALLOWED_CONTENT_LANGUAGES = ['de', 'en'] as const;
export type ReportContentLanguage = (typeof FORMAL_REPORT_ALLOWED_CONTENT_LANGUAGES)[number];

/** `de` veya `en` değilse false (ör. `tr` UI için formal metin dili yine de `de`/`en` olabilir). */
export function isReportContentLanguage(value: unknown): value is ReportContentLanguage {
  return value === 'de' || value === 'en';
}

/**
 * Hem DE hem EN sunulduğunda önce hangi dilin tercih edileceği (UI yerine “formal içerik” tercihi).
 * `en` arayüzü → önce İngilizce; `de` / `tr` / diğer → önce Almanca (TR arayüzde formal metin yine DE/EN).
 */
export function preferredReportContentLanguage(uiLocale: TextLocale): ReportContentLanguage {
  return uiLocale === 'en' ? 'en' : 'de';
}
