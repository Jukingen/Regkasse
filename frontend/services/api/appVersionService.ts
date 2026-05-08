import { apiClient } from './config';

/**
 * Backend cevabı: `GET /api/app/version`. Anonim çağrılır; kullanıcı oturumu açmadan da
 * çalışmalı (eski/expired token ile bile sürüm sorgulanabilmeli).
 */
export interface AppVersionResponse {
  /** Yayınlanan en son APK için monoton versionCode (tamsayı). */
  latestVersionCode: number;
  /** UI'da gösterilen sürüm adı, ör. "1.0.1". Karşılaştırma için KULLANILMAZ. */
  latestVersionName: string;
  /** İmzalı APK'nın doğrudan indirme URL'i (HTTPS önerilir). */
  downloadUrl?: string | null;
  /** Opsiyonel release-notes URL'i. */
  releaseNotesUrl?: string | null;
  /** True ise istemci güncellemeyi zorunlu olarak işlemeli (snooze izni opsiyonel). */
  mandatory?: boolean;
  /** versionCode bu değerin altındaki istemciler çalışmayı reddetmeli. */
  minimumSupportedVersionCode?: number;
  /** APK'nın SHA-256 hex'i (lowercase). İndirme sonrası doğrulama için opsiyonel. */
  sha256?: string | null;
  /** APK boyutu (byte). İlerleme/depolama UX'i için opsiyonel. */
  sizeBytes?: number | null;
  /** Sunucunun cevap verdiği UTC zaman damgası. */
  serverTimeUtc?: string;
}

/**
 * En son yayınlanmış POS APK meta verisini getirir.
 * Backend endpoint'i anonim olduğu için Authorization header gerekli değil.
 */
export async function getLatestAppVersion(): Promise<AppVersionResponse> {
  return apiClient.get<AppVersionResponse>('/app/version');
}
