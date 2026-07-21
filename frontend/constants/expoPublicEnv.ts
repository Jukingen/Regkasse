/**
 * Expo public env helpers.
 *
 * Metro only inlines **static** `process.env.EXPO_PUBLIC_*` property access.
 * Dynamic keys like `process.env[name]` are NOT substituted at bundle time —
 * callers must pass values already read via static property access.
 */

export function trimExpoPublicEnv(value: string | undefined | null): string | undefined {
  const trimmed = value?.trim();
  return trimmed ? trimmed : undefined;
}

/** Known POS public env keys (documentation + tests). Values must still be read statically. */
export const EXPO_PUBLIC_ENV_KEYS = {
  apiBaseUrl: 'EXPO_PUBLIC_API_BASE_URL',
  apiUrlLegacy: 'EXPO_PUBLIC_API_URL',
  devTenantId: 'EXPO_PUBLIC_DEV_TENANT_ID',
  adminBaseUrl: 'EXPO_PUBLIC_ADMIN_BASE_URL',
  licenseExtensionUrl: 'EXPO_PUBLIC_LICENSE_EXTENSION_URL',
  appSurface: 'EXPO_PUBLIC_APP_SURFACE',
  simulateNetworkDelayMs: 'EXPO_PUBLIC_SIMULATE_NETWORK_DELAY_MS',
} as const;

/**
 * Snapshot of public env used by POS config surfaces.
 * Uses static property access so Expo/Metro can inline values at bundle time.
 */
export function getExpoPublicEnvSnapshot() {
  return {
    apiBaseUrl: trimExpoPublicEnv(process.env.EXPO_PUBLIC_API_BASE_URL),
    apiUrlLegacy: trimExpoPublicEnv(process.env.EXPO_PUBLIC_API_URL),
    devTenantId: trimExpoPublicEnv(process.env.EXPO_PUBLIC_DEV_TENANT_ID),
    adminBaseUrl: trimExpoPublicEnv(process.env.EXPO_PUBLIC_ADMIN_BASE_URL),
    licenseExtensionUrl: trimExpoPublicEnv(process.env.EXPO_PUBLIC_LICENSE_EXTENSION_URL),
    appSurface: trimExpoPublicEnv(process.env.EXPO_PUBLIC_APP_SURFACE),
    simulateNetworkDelayMs: trimExpoPublicEnv(process.env.EXPO_PUBLIC_SIMULATE_NETWORK_DELAY_MS),
  };
}
