import { File, Paths } from 'expo-file-system';
import { Platform } from 'react-native';

import { APP_VERSION_CODE, APP_VERSION_NAME } from './currentAppVersion';
import { shareDocumentAsync, ShareUnavailableError } from '../../utils/expoPrintShare';
import { getLatestAppVersion, type AppVersionResponse } from '../api/appVersionService';

/**
 * Mevcut versiyon ile backend'in raporladığı en son versiyon arasındaki kararı kapsayan
 * tip. UI bu nesneyi kullanıcıya gösterir; iş akışını başlatma sorumluluğu yine UI'da.
 */
export interface AppUpdateCheckResult {
  /** Karşılaştırma sonucu: yeni sürüm var mı? */
  hasUpdate: boolean;
  /** İstemci HARD-BLOCK floor'unun altında mı (uygulama açılmamalı)? */
  blocked: boolean;
  /** Backend `mandatory: true` döndürdü ve yeni sürüm var. */
  mandatory: boolean;
  /** İstemcinin kendi `versionCode`'u. */
  currentVersionCode: number;
  /** İstemcinin kendi `versionName`'i (UI'da gösterilir). */
  currentVersionName: string;
  /** Sunucu cevabının ham hali (downloadUrl, sizeBytes, sha256, …). */
  remote: AppVersionResponse;
}

/** İndirme/install kapsamlı tip ile tek tip hata atmak için yardımcı sınıf. */
export class AppUpdateError extends Error {
  constructor(
    public readonly code: AppUpdateErrorCode,
    message: string
  ) {
    super(message);
    this.name = 'AppUpdateError';
  }
}

export type AppUpdateErrorCode =
  | 'unsupported_platform'
  | 'no_download_url'
  | 'download_failed'
  | 'sharing_unavailable'
  | 'install_failed';

/**
 * Backend'den en son sürümü sorgular ve mevcut sürüme göre karşılaştırma yapar.
 * SADECE Android için anlamlıdır; web/iOS'ta `hasUpdate=false` döner.
 */
export async function checkForAppUpdate(): Promise<AppUpdateCheckResult> {
  const remote = await getLatestAppVersion();
  const isAndroid = Platform.OS === 'android';

  const minSupported = remote.minimumSupportedVersionCode ?? 0;
  const blocked = isAndroid && minSupported > 0 && APP_VERSION_CODE < minSupported;
  const hasUpdate = isAndroid && remote.latestVersionCode > APP_VERSION_CODE;

  return {
    hasUpdate,
    blocked,
    mandatory: hasUpdate && Boolean(remote.mandatory),
    currentVersionCode: APP_VERSION_CODE,
    currentVersionName: APP_VERSION_NAME,
    remote,
  };
}

/**
 * APK'yı yerel cache klasörüne indirir. Permission gerektirmez (cache directory app-private).
 * Uses the Expo SDK 56+ `expo-file-system` modern API
 * (`File.downloadFileAsync(url, destination)` into `Paths.cache`).
 *
 * Dönen değer indirilen dosyanın URI'si (`file://…`); `launchApkInstaller`'a doğrudan
 * geçirilebilir. URI string döndürerek `File` sınıfı / DOM `File` arasındaki tip
 * çakışmasından kaçınıyoruz.
 */
export async function downloadApk(url: string, fileName?: string): Promise<string> {
  if (Platform.OS !== 'android') {
    throw new AppUpdateError('unsupported_platform', 'APK download only supported on Android.');
  }
  if (!url || typeof url !== 'string') {
    throw new AppUpdateError('no_download_url', 'No downloadUrl returned from server.');
  }

  const safeName = sanitizeFileName(fileName ?? deriveFileNameFromUrl(url));
  const target = new File(Paths.cache, safeName);

  // İdempotent: aynı isimle eski bir indirme varsa üzerine yaz.
  try {
    if (target.exists) {
      target.delete();
    }
  } catch {
    // ignore: dosya yoksa veya silinemiyorsa download yine de overwrite/idempotent ile dener
  }

  try {
    const result = await File.downloadFileAsync(url, target, { idempotent: true });
    return (result as { uri: string }).uri;
  } catch (err) {
    const message = err instanceof Error ? err.message : 'Unknown download error.';
    throw new AppUpdateError('download_failed', `APK download failed: ${message}`);
  }
}

/**
 * İndirilen APK'yı sistem yükleyicisine paslar. `react-native-permissions` yerine
 * `expo-sharing` kullanılır: Android'de "Open with…" diyaloğunu açar; kullanıcı
 * "Package Installer" seçer. "Unknown sources" izni hâlâ tablette aktif olmalı.
 */
export async function launchApkInstaller(fileUri: string): Promise<void> {
  if (Platform.OS !== 'android') {
    throw new AppUpdateError('unsupported_platform', 'APK install only supported on Android.');
  }
  if (!fileUri) {
    throw new AppUpdateError('install_failed', 'No downloaded APK URI to install.');
  }

  try {
    await shareDocumentAsync(fileUri, {
      mimeType: 'application/vnd.android.package-archive',
      dialogTitle: 'Regkasse POS aktualisieren',
    });
  } catch (err) {
    if (err instanceof ShareUnavailableError) {
      throw new AppUpdateError(
        'sharing_unavailable',
        'System sharing is not available on this device.'
      );
    }
    const message = err instanceof Error ? err.message : 'Unknown installer error.';
    throw new AppUpdateError('install_failed', `Failed to launch installer: ${message}`);
  }
}

function deriveFileNameFromUrl(url: string): string {
  try {
    const path = new URL(url).pathname;
    const last = path.split('/').pop();
    if (last?.toLowerCase().endsWith('.apk')) return last;
  } catch {
    // not a parseable URL; fall through
  }
  return 'regkasse-update.apk';
}

function sanitizeFileName(name: string): string {
  // path traversal ve sürpriz karakterleri eler; .apk uzantısı garanti
  const trimmed = name
    .replace(/[\\/:*?"<>|\s]+/g, '_')
    .replace(/^\.+/, '')
    .slice(0, 96);
  if (!trimmed) return 'regkasse-update.apk';
  return trimmed.toLowerCase().endsWith('.apk') ? trimmed : `${trimmed}.apk`;
}
