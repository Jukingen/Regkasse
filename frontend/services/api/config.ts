import axios, { type AxiosRequestConfig, type InternalAxiosRequestConfig } from 'axios';
import { jwtDecode } from 'jwt-decode';

import {
  createApiAbortController,
  getNetworkRetryCount,
  networkRetryDelayMs,
  shouldRetryAxiosNetworkError,
  sleep,
  withIncrementedRetryCount,
} from './axiosReliability';
import { applyCsrfHeaders, ensureCsrfToken, requestNeedsCsrf } from './csrf';
import {
  API_BASE_URL as CONFIGURED_API_BASE_URL,
  stripDevTenantQueryFromBaseUrl,
} from '../../config';
import { applyDevNetworkDelayIfConfigured } from '../../src/config/devFlags';
import { sessionManager } from '../session/sessionManager';
import { applyTenantHeader, resolveEffectiveTenantSlug } from '../tenant/devTenant';
import { tenantStorage, TENANT_HTTP_HEADER } from '../tenant/tenantStorage';

const isDev = __DEV__;

// Platform-aware API URL from main config
export const API_BASE_URL = CONFIGURED_API_BASE_URL;

/** Applies tenant-specific API base URL after license activation (no-op when unchanged). */
export function applyStoredApiBaseUrl(url: string): void {
  const normalized = stripDevTenantQueryFromBaseUrl(url);
  if (!normalized || axiosInstance.defaults.baseURL === normalized) {
    return;
  }
  axiosInstance.defaults.baseURL = normalized;
  if (isDev) {
    console.log('🔧 API base URL updated from tenant bootstrap:', normalized);
  }
}

/** Restores configured default API base URL (e.g. after logout). */
export function resetApiBaseUrlToConfigured(): void {
  if (axiosInstance.defaults.baseURL !== CONFIGURED_API_BASE_URL) {
    axiosInstance.defaults.baseURL = CONFIGURED_API_BASE_URL;
  }
}

/**
 * Ensures axios base URL has no <c>?tenant=</c> suffix (dev tenant uses {@link TENANT_HTTP_HEADER} per request).
 * Call after dev tenant switch or on auth bootstrap to clear legacy mis-built base URLs.
 */
export async function hydrateDevTenantApiBaseUrl(): Promise<void> {
  if (!isDev) return;
  const next = stripDevTenantQueryFromBaseUrl(CONFIGURED_API_BASE_URL);
  if (axiosInstance.defaults.baseURL !== next) {
    axiosInstance.defaults.baseURL = next;
    if (isDev) {
      console.log('🔧 API base URL normalized (tenant via X-Tenant-Id):', next);
    }
  }
}

/** Resolves slug from login/license persistence, with dev override when applicable. */
async function resolveRequestTenantSlug(): Promise<string | null> {
  const persistedSlug = await tenantStorage.getTenantSlug();
  return await resolveEffectiveTenantSlug(persistedSlug);
}

/** Adds {@link TENANT_HTTP_HEADER} when a tenant slug is available. */
export async function addTenantHeader(
  config: InternalAxiosRequestConfig
): Promise<InternalAxiosRequestConfig> {
  const tenantSlug = await resolveRequestTenantSlug();
  if (!tenantSlug) {
    return config;
  }

  config.headers = applyTenantHeader(config.headers, tenantSlug) as typeof config.headers;

  return config;
}

/** Headers for raw <c>fetch()</c> (axios request interceptor adds tenant header automatically). */
export async function resolveTenantFetchHeaders(
  headers: Record<string, string> = {}
): Promise<Record<string, string>> {
  const tenantSlug = await resolveRequestTenantSlug();
  if (!tenantSlug) return headers;
  return applyTenantHeader(headers, tenantSlug) as Record<string, string>;
}

if (isDev) {
  console.log('🔧 API Services - Using API Base URL:', API_BASE_URL);
}

// Token yönetimi için yardımcı fonksiyonlar
const TokenManager = {
  // Token'ın geçerlilik süresini kontrol et
  isTokenExpired: (token: string): boolean => {
    try {
      // Eğer token 'Bearer ' ile başlıyorsa kaldır
      const cleanToken = token.startsWith('Bearer ') ? token.substring(7) : token;

      const decoded = jwtDecode(cleanToken);
      const currentTime = Date.now() / 1000;
      const isExpired = decoded.exp ? decoded.exp < currentTime : true;

      return isExpired;
    } catch {
      return true;
    }
  },

  // Token'dan kullanıcı bilgilerini çıkar
  getTokenInfo: (token: string) => {
    try {
      // Eğer token 'Bearer ' ile başlıyorsa kaldır
      const cleanToken = token.startsWith('Bearer ') ? token.substring(7) : token;
      return jwtDecode(cleanToken);
    } catch {
      return null;
    }
  },

  // Güvenli token saklama (never log or broadcast the JWT value)
  storeToken: async (token: string) => {
    try {
      const cleanToken = sessionManager.normalizeToken(token);
      await sessionManager.persistSession({ token: cleanToken });
      if (isDev) {
        console.log('Token stored successfully.');
      }

      try {
        if (typeof window !== 'undefined' && typeof window.dispatchEvent === 'function') {
          // Detail intentionally omits the token — listeners should re-read from sessionManager.
          window.dispatchEvent(new CustomEvent('auth-token-updated'));
        }
      } catch (eventError) {
        console.warn('Token update event dispatch failed:', eventError);
      }
    } catch (error) {
      if (isDev) {
        console.error('Token storage failed:', error);
      }
    }
  },

  // Token'ları temizle
  clearTokens: async () => {
    try {
      await sessionManager.clearSession();
    } catch (error) {
      if (isDev) {
        console.error('Token cleanup failed:', error);
      }
    }
  },
};

// Axios instance oluştur
const axiosInstance = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
  timeout: 30000, // RKSV işlemleri için optimize edildi
  // Credentials so web builds can store the CSRF cookie; native uses X-CSRF-COOKIE mirror.
  withCredentials: true,
});

const REFRESH_HEADER = 'x-auth-refresh-retry';

/** Paths that must never send a Bearer token (stale JWT can fail AllowAnonymous via JwtBearer). */
function isAnonymousAuthPath(url: string | undefined): boolean {
  if (!url) return false;
  const path = url.split('?')[0]?.toLowerCase() ?? '';
  // Exact liveness only — do not match /tse/health or /pos/offline/health.
  const isLivenessHealth =
    path === '/health' || path === '/api/health' || path.endsWith('/api/health');
  return (
    path.includes('/auth/login') ||
    path.includes('/auth/refresh') ||
    path.includes('/license/activate') ||
    path.includes('/public/online-orders') ||
    path.includes('/webhooks/stripe') ||
    isLivenessHealth
  );
}

function requestHadAuthorization(
  config: { headers?: Record<string, unknown> } | undefined
): boolean {
  const headers = config?.headers;
  if (!headers) return false;
  const value =
    headers.Authorization ??
    headers.authorization ??
    (typeof (headers as { get?: (key: string) => unknown }).get === 'function'
      ? (headers as { get: (key: string) => unknown }).get('Authorization')
      : undefined);
  return typeof value === 'string' && value.trim().length > 0;
}

/** Map known backend Turkish literals to German for POS UI (backend unchanged). */
function translateKnownTurkishApiErrorMessages(data: unknown): void {
  if (!data || typeof data !== 'object' || Array.isArray(data)) return;
  const body = data as Record<string, unknown>;
  const msg = body.message;
  if (typeof msg !== 'string') return;

  const turkishToGerman: [string, string][] = [
    ['Kullanıcı bulunamadı', 'Benutzername oder E-Mail nicht gefunden.'],
    ['Geçersiz şifre', 'Falsches Passwort. Bitte versuchen Sie es erneut.'],
    ['Hesap aktif değil', 'Ihr Konto ist gesperrt. Bitte kontaktieren Sie Ihren Administrator.'],
    [
      'Bu kullanıcı bu uygulama için yetkili değil.',
      'Sie haben keine Berechtigung für die POS-App. Bitte kontaktieren Sie Ihren Administrator.',
    ],
  ];

  for (const [turkish, german] of turkishToGerman) {
    if (msg.includes(turkish)) {
      body.message = msg.split(turkish).join(german);
      return;
    }
  }
}

/** Dev-only error log — never includes Authorization, cookies, or request/response bodies. */
function logSafeApiError(context: string, error: unknown): void {
  if (!isDev) return;
  if (!error || typeof error !== 'object') {
    console.error(context, { message: String(error) });
    return;
  }
  const ax = error as {
    message?: string;
    code?: string;
    name?: string;
    response?: { status?: number };
    config?: { url?: string; method?: string };
    status?: number;
  };
  const status = ax.response?.status ?? ax.status;
  if (status === 401) return;
  console.error(context, {
    status,
    code: ax.code,
    method: ax.config?.method,
    url: ax.config?.url,
    message: ax.message,
  });
}

// Request interceptor — attach Bearer only for authenticated, non-expired sessions.
axiosInstance.interceptors.request.use(
  async (config) => {
    if (config.signal?.aborted) {
      throw new axios.CanceledError('Request aborted before send');
    }

    await applyDevNetworkDelayIfConfigured();

    await addTenantHeader(config);

    config.headers = config.headers ?? {};
    config.headers['Accept-Language'] = 'de';

    if (requestNeedsCsrf(config.method, config.url)) {
      try {
        const base =
          typeof config.baseURL === 'string' && config.baseURL.length > 0
            ? config.baseURL
            : API_BASE_URL;
        const csrf = await ensureCsrfToken(base);
        const bag: Record<string, string> = {};
        applyCsrfHeaders(bag, csrf);
        for (const [key, value] of Object.entries(bag)) {
          config.headers[key] = value;
        }
        config.withCredentials = true;
      } catch {
        if (isDev) {
          console.warn('[API] CSRF token bootstrap failed');
        }
      }
    }

    // Skip auth for public endpoints (stale JWT must never be sent).
    if (isAnonymousAuthPath(config.url)) {
      if (config.headers.Authorization) {
        delete config.headers.Authorization;
      }
      if (config.headers.authorization) {
        delete config.headers.authorization;
      }
      return config;
    }

    const token = await sessionManager.getAccessToken();
    if (token && !TokenManager.isTokenExpired(token)) {
      config.headers.Authorization = `Bearer ${token}`;
      return config;
    }

    // Expired / missing token: never attach Authorization (avoids login-screen 401 loops).
    if (token && TokenManager.isTokenExpired(token)) {
      if (isDev) {
        console.log('Token expired, clearing...');
      }
      await TokenManager.clearTokens();
      if (typeof window !== 'undefined' && window.dispatchEvent) {
        window.dispatchEvent(new CustomEvent('AUTH_SESSION_EXPIRED'));
      }
    }

    if (config.headers.Authorization) {
      delete config.headers.Authorization;
    }
    if (config.headers.authorization) {
      delete config.headers.authorization;
    }
    return config;
  },
  (error) => Promise.reject(error)
);

// Response interceptor - Hata yönetimi, network retry, token yenileme
axiosInstance.interceptors.response.use(
  (response) => {
    return response.data;
  },
  async (error) => {
    if (error.response?.data) {
      translateKnownTurkishApiErrorMessages(error.response.data);
    }

    // Transport failures on idempotent GETs: exponential backoff (never payment/auth POSTs).
    if (shouldRetryAxiosNetworkError(error) && error.config) {
      const nextConfig = withIncrementedRetryCount(error.config);
      const attempt = getNetworkRetryCount(nextConfig);
      const delayMs = networkRetryDelayMs(attempt);
      if (isDev) {
        console.warn('[API] Network retry', {
          attempt,
          delayMs,
          method: error.config.method,
          url: error.config.url,
        });
      }
      await sleep(delayMs);
      if (nextConfig.signal?.aborted) {
        return await Promise.reject(error);
      }
      return await axiosInstance.request(nextConfig);
    }

    logSafeApiError('❌ API error:', error);

    // 401 Unauthorized - Token geçersiz
    if (error.response?.status === 401) {
      if (isDev) {
        console.log('⚠️ Unauthorized error (401)');
      }

      const originalConfig = error.config || {};
      const hadAuthHeader = requestHadAuthorization(originalConfig);

      // Unauthenticated call to a protected endpoint (login screen / bootstrap) — do not logout/refresh.
      if (!hadAuthHeader) {
        return await Promise.reject(error);
      }

      const token = await sessionManager.getAccessToken();
      let isExpired = true;

      if (token) {
        isExpired = TokenManager.isTokenExpired(token);
        if (isDev) {
          console.log('[API] 401 received. Local Token Expired:', isExpired);
        }
      }

      const wasRetried = !!originalConfig.headers?.[REFRESH_HEADER];

      if (!wasRetried) {
        const refreshedToken = await sessionManager.refreshAccessToken(async (refreshToken) => {
          const res = await axiosInstance.post<{ token: string }>(
            '/auth/refresh',
            { refreshToken },
            { headers: { [REFRESH_HEADER]: '1' } }
          );
          return res.data;
        });

        if (refreshedToken) {
          originalConfig.headers = {
            ...(originalConfig.headers || {}),
            Authorization: `Bearer ${refreshedToken}`,
            [REFRESH_HEADER]: '1',
          };
          return await axiosInstance(originalConfig);
        }
      }

      if (isExpired || wasRetried) {
        if (isDev) {
          console.log('⚠️ Session invalid/refresh failed, dispatching expiration event...');
        }
        if (typeof window !== 'undefined' && window.dispatchEvent) {
          const event = new CustomEvent('AUTH_SESSION_EXPIRED');
          window.dispatchEvent(event);
        }
      } else if (isDev) {
        console.warn('⚠️ Server returned 401 but token is locally valid.');
        console.warn('⚠️ This might be server time skew or invalid signature.');
      }

      return await Promise.reject(error);
    }

    // 403 Forbidden - Yetkisiz erişim
    if (error.response?.status === 403) {
      if (isDev) {
        console.log('⛔ Forbidden error (403) - insufficient permissions');
      }
    }

    // 5xx — surface status for UI; do not auto-retry (may not be idempotent).
    if (error.response?.status && error.response.status >= 500) {
      if (isDev) {
        console.warn('🔥 Server error', { status: error.response.status, url: error.config?.url });
      }
    }

    // Normalized error for callers (no Axios internals / no secret headers).
    throw {
      status: error.response?.status,
      data: error.response?.data,
      message: error.message,
      code: error.code,
      isNetworkError: !error.response,
      isCanceled: error.code === 'ERR_CANCELED' || error.name === 'CanceledError',
    };
  }
);

function getApiErrorStatus(error: unknown): number | undefined {
  if (!error || typeof error !== 'object') return undefined;
  const candidate = error as { response?: { status?: number }; status?: number };
  return candidate.response?.status ?? candidate.status;
}

function logApiClientError(method: string, error: unknown): void {
  if (!isDev) return;
  // 401 is common on login/bootstrap; response interceptor already handles refresh/logout.
  if (getApiErrorStatus(error) === 401) return;
  logSafeApiError(`${method} request failed:`, error);
}

// API client — pass AxiosRequestConfig.signal (AbortController) to cancel in-flight calls.
export const apiClient = {
  get: async <T>(url: string, config?: AxiosRequestConfig): Promise<T> => {
    try {
      const response = await axiosInstance.get<T>(url, config);
      return response as T;
    } catch (error) {
      logApiClientError('GET', error);
      throw error;
    }
  },

  post: async <T>(url: string, data?: unknown, config?: AxiosRequestConfig): Promise<T> => {
    try {
      const response = await axiosInstance.post<T>(url, data, config);
      return response as T;
    } catch (error) {
      logApiClientError('POST', error);
      throw error;
    }
  },

  put: async <T>(url: string, data?: unknown, config?: AxiosRequestConfig): Promise<T> => {
    try {
      const response = await axiosInstance.put<T>(url, data, config);
      return response as T;
    } catch (error) {
      logApiClientError('PUT', error);
      throw error;
    }
  },

  delete: async <T>(url: string, config?: AxiosRequestConfig): Promise<T> => {
    try {
      const response = await axiosInstance.delete<T>(url, config);
      return response as T;
    } catch (error) {
      logApiClientError('DELETE', error);
      throw error;
    }
  },
};

export { TokenManager, axiosInstance, createApiAbortController };
export type { AxiosRequestConfig };
