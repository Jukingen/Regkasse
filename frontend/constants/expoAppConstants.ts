import Constants from 'expo-constants';

/**
 * Safe Expo app-config access for SDK 56+.
 *
 * Prefer {@link Constants.expoConfig}. Do **not** use deprecated
 * `Constants.manifest` (throws warnings / is null under EAS Update).
 */

/** Embedded Expo config from app.json / app.config (classic + modern manifests). */
export function getExpoConfig() {
  return Constants.expoConfig ?? null;
}

/**
 * App version string for UI / update checks.
 * Order: expoConfig.version → nativeAppVersion → fallback.
 */
export function getExpoAppVersionName(fallback = '1.0.0'): string {
  const fromConfig = Constants.expoConfig?.version?.trim();
  if (fromConfig) return fromConfig;

  const native = readOptionalString((Constants as { nativeAppVersion?: unknown }).nativeAppVersion);
  if (native) return native;

  return fallback;
}

/** Expo slug from config (useful for diagnostics; not a tenant slug). */
export function getExpoAppSlug(): string | null {
  const slug = Constants.expoConfig?.slug?.trim();
  return slug || null;
}

/** True when running inside Expo Go / a store-client style host. */
export function isExpoStoreClient(): boolean {
  return Constants.executionEnvironment === 'storeClient';
}

function readOptionalString(value: unknown): string | null {
  if (typeof value !== 'string') return null;
  const trimmed = value.trim();
  return trimmed || null;
}
