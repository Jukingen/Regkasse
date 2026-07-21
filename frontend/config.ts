import { Platform } from 'react-native';

import { trimExpoPublicEnv } from './constants/expoPublicEnv';

const isDev = __DEV__;
const API_BASE_URL_ENV = 'EXPO_PUBLIC_API_BASE_URL';
const LEGACY_API_URL_ENV = 'EXPO_PUBLIC_API_URL';

const normalizeEnv = (value?: string) => trimExpoPublicEnv(value);

/** Strips dev tenant query params accidentally baked into a stored base URL. */
export function stripDevTenantQueryFromBaseUrl(baseUrl: string): string {
  const trimmed = baseUrl.trim().replace(/\/+$/, '');
  const q = trimmed.indexOf('?');
  if (q === -1) return trimmed;
  return trimmed.slice(0, q);
}

// Platform-aware API URL configuration
const getApiBaseUrl = () => {
  // 1. Priority: Check EXPO_PUBLIC_API_BASE_URL (Preferred)
  // Static process.env access so Expo/Metro can inline at bundle time.
  const configuredApiBaseUrl = normalizeEnv(process.env.EXPO_PUBLIC_API_BASE_URL);
  if (configuredApiBaseUrl) {
    if (isDev) {
      console.log(`🌐 Using API URL from ${API_BASE_URL_ENV}:`, configuredApiBaseUrl);
    }
    return stripDevTenantQueryFromBaseUrl(configuredApiBaseUrl);
  }

  // 2. Check legacy env var for local development only
  const legacyApiUrl = normalizeEnv(process.env.EXPO_PUBLIC_API_URL);
  if (legacyApiUrl) {
    if (!isDev) {
      throw new Error(
        `${API_BASE_URL_ENV} must be configured for production builds. ${LEGACY_API_URL_ENV} is only supported during development.`
      );
    }

    console.warn(
      `[config] ${API_BASE_URL_ENV} is missing. Using legacy ${LEGACY_API_URL_ENV} for local development only: ${legacyApiUrl}`
    );
    return stripDevTenantQueryFromBaseUrl(legacyApiUrl);
  }

  if (!isDev) {
    throw new Error(`${API_BASE_URL_ENV} must be configured for production builds.`);
  }

  if (Platform.OS === 'web') {
    const backendApiUrl = 'http://localhost:5184/api';
    console.warn(
      `[config] ${API_BASE_URL_ENV} is missing. Using web-only development fallback: ${backendApiUrl}. Set ${API_BASE_URL_ENV} for beta or device testing.`
    );
    return backendApiUrl;
  }

  throw new Error(
    `${API_BASE_URL_ENV} is required for native development builds. Set it in frontend/.env, for example ${API_BASE_URL_ENV}=http://YOUR_DEV_MACHINE_IP:5184/api. No LAN IP fallback is used.`
  );
};

export const API_BASE_URL = getApiBaseUrl();

if (isDev) {
  console.log('🔧 Final API_BASE_URL:', API_BASE_URL);
}
