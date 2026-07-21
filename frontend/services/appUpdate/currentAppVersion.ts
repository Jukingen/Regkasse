/**
 * Yerel POS uygulama sürümü meta verisi.
 *
 * Bu dosya, in-app güncelleme denetleyicisi (settings ▸ "Auf Updates prüfen") için
 * runtime sürüm karşılaştırmasının TEK kaynağıdır.
 *
 * - `APP_VERSION_NAME`: `Constants.expoConfig.version` (`app.json` → `expo.version`)
 *   ile hizalanır; config yoksa fallback kullanılır. Deprecated `Constants.manifest` kullanılmaz.
 * - `APP_VERSION_CODE`: monoton tamsayı — her release'de (yama dahil) +1 bump et;
 *   EAS `android.versionCode` ile senkron tutulmalıdır.
 */

import { getExpoAppVersionName } from '../../constants/expoAppConstants';

/** Kullanıcıya gösterilen sürüm adı (`app.json` → `expo.version`). */
export const APP_VERSION_NAME = getExpoAppVersionName('1.0.0');

/**
 * Backend'den dönen `latestVersionCode` ile karşılaştırılan monoton tamsayı.
 * Her release'de (yama dahil) +1 bump et; geriye gitmemeli.
 */
export const APP_VERSION_CODE = 1;
