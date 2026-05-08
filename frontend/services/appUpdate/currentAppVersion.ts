/**
 * Yerel POS uygulama sürümü meta verisi.
 *
 * Bu dosya, in-app güncelleme denetleyicisi (settings ▸ "Auf Updates prüfen") için
 * runtime sürüm karşılaştırmasının TEK kaynağıdır. `app.json` (`expo.version`,
 * `expo.runtimeVersion` ve EAS tarafından enjekte edilen Android `versionCode`) ile
 * SENKRON tutulmalıdır. Yeni bir release yayımlarken her iki tarafı da bump et.
 */

/** Kullanıcıya gösterilen sürüm adı. `app.json` -> `expo.version` ile aynı olmalı. */
export const APP_VERSION_NAME = '1.0.0';

/**
 * Backend'den dönen `latestVersionCode` ile karşılaştırılan monoton tamsayı.
 * Her release'de (yama dahil) +1 bump et; geriye gitmemeli.
 */
export const APP_VERSION_CODE = 1;
