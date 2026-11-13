import { Platform } from 'react-native';

import { isExpoStoreClient } from '../constants/expoAppConstants';

const LOOPBACK_HOSTS = new Set(['localhost', '127.0.0.1', '[::1]', '::1']);

export const DEV_LOOPBACK_API_WARNING_DE =
  'Entwicklung: EXPO_PUBLIC_API_BASE_URL zeigt auf localhost. Physische Geräte, Expo Go und der Android-Emulator erreichen den API-Server nicht. Setzen Sie die LAN-IP dieses Rechners, z. B. http://192.168.x.x:5184/api.';

export function isLoopbackApiBaseUrl(apiBaseUrl: string): boolean {
  const trimmed = apiBaseUrl.trim();
  if (!trimmed) return false;
  try {
    const host = new URL(trimmed).hostname.toLowerCase();
    return LOOPBACK_HOSTS.has(host);
  } catch {
    return /localhost|127\.0\.0\.1|::1/i.test(trimmed);
  }
}

export function shouldShowDevLoopbackApiWarning(input: {
  isDev: boolean;
  platformOS: string;
  apiBaseUrl: string;
}): boolean {
  if (!input.isDev) return false;
  if (input.platformOS === 'web') return false;
  return isLoopbackApiBaseUrl(input.apiBaseUrl);
}

/** Console hint for Expo Go / physical device / Android emulator developers. */
export function logDevLoopbackApiWarningIfNeeded(apiBaseUrl: string): void {
  if (
    !shouldShowDevLoopbackApiWarning({
      isDev: typeof __DEV__ !== 'undefined' && __DEV__,
      platformOS: Platform.OS,
      apiBaseUrl,
    })
  ) {
    return;
  }

  const expoGoHint = isExpoStoreClient() ? ' (Expo Go)' : '';
  console.warn(`[POS API]${expoGoHint} ${DEV_LOOPBACK_API_WARNING_DE}`);
}
