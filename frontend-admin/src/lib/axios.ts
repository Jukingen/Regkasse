import axios, { AxiosInstance, AxiosRequestConfig } from 'axios';

import { authStorage } from '@/features/auth/services/authStorage';
import { resolveTenantSlugForApiRequest } from '@/features/auth/services/devTenant';
import { TENANT_HTTP_HEADER } from '@/features/auth/services/tenantStorage';
import { isPublicAuthEntryPath } from '@/features/auth/utils/isPublicAuthEntryPath';
import { getStoredLanguage } from '@/i18n/languageStorage';
import { showAntdError } from '@/lib/antdAppBridge';
import {
  CSRF_HEADER,
  applyCsrfHeaders,
  clearCsrfTokenCache,
  ensureCsrfToken,
  isCsrfForbiddenMessage,
  requestNeedsCsrf,
} from '@/lib/csrf';
import { reportApiMetric } from '@/lib/monitoring/reportApiMetric';
import { reportAxiosErrorToSentry } from '@/lib/monitoring/reportToSentry';
import { cookieService } from '@/services/cookieService';
import { technicalConsole } from '@/shared/dev/technicalConsole';
import {
  getForbiddenMessage,
  mapRequiredPolicyToReasonCode,
} from '@/shared/errors/forbiddenMessages';

const isDev = process.env.NODE_ENV === 'development';

/** Request start times for latency metrics (no PII). */
const apiRequestStarts = new WeakMap<object, number>();

function markApiRequestStart(config: AxiosRequestConfig): void {
  apiRequestStarts.set(config, typeof performance !== 'undefined' ? performance.now() : Date.now());
}

function takeApiRequestDurationMs(config: AxiosRequestConfig | undefined): number | null {
  if (!config) {
    return null;
  }
  const start = apiRequestStarts.get(config);
  if (start == null) {
    return null;
  }
  apiRequestStarts.delete(config);
  const end = typeof performance !== 'undefined' ? performance.now() : Date.now();
  return Math.max(0, end - start);
}

function isRequestCanceled(error: unknown): boolean {
  if (axios.isCancel(error)) {
    return true;
  }
  const err = error as {
    message?: string;
    code?: string;
    name?: string;
    config?: { signal?: AbortSignal };
  };
  if (err.code === 'ERR_CANCELED' || err.code === 'ECONNABORTED') {
    return true;
  }
  if (err.name === 'AbortError' || err.name === 'CanceledError') {
    return true;
  }
  const message = err.message;
  if (message === 'canceled' || message === 'Query was cancelled') {
    return true;
  }
  if (err.config?.signal?.aborted) {
    return true;
  }
  return false;
}

/** Expected read-only / grace license enforcement — not an application fault. */
function isExpectedLicenseWriteBlock403(
  status: number | undefined,
  data: { error?: string; code?: string; message?: string } | undefined,
  serverMessage: string | null
): boolean {
  if (status !== 403) return false;
  const errorToken = typeof data?.error === 'string' ? data.error : '';
  const codeToken = typeof data?.code === 'string' ? data.code : '';
  const haystack = `${errorToken} ${codeToken} ${serverMessage ?? ''}`.toLowerCase();
  return haystack.includes('license') || haystack.includes('lizenz');
}

/** Optional settings rows may be absent until tenant system settings are seeded. */
function isOptionalSettingsNotFound404(status: number | undefined, url: string): boolean {
  if (status !== 404) {
    return false;
  }
  return /\/api\/Settings\/tax-rates\b/i.test(url);
}

const configuredBaseUrl = process.env.NEXT_PUBLIC_API_BASE_URL;
const baseURL = configuredBaseUrl || (isDev ? 'http://localhost:5184' : '');

if (!baseURL) {
  throw new Error('NEXT_PUBLIC_API_BASE_URL must be configured for non-development environments.');
}

// Extended window interface for global singleton in Dev
declare global {
  var _axiosInstance: AxiosInstance | undefined;
  var _refreshPromise: Promise<string | null> | null | undefined;
}

type RefreshTokenResponse = {
  token?: string;
  refreshToken?: string;
};

export type RefreshAccessTokenOptions = {
  /** When set, backend rebinds session + JWT `tenant_id` (dev tenant switcher). */
  tenantId?: string | null;
  /**
   * When true (default), clear stored tokens if refresh fails (401 recovery).
   * Set false for tenant switch so a failed rebind does not log the user out.
   */
  clearOnFailure?: boolean;
};

/** Shared in-flight refresh — prevents parallel `/api/Auth/refresh` (reuse detection). */
let refreshPromise: Promise<string | null> | null = null;

/**
 * Rotates refresh token and persists the new access token.
 * Optional `tenantId` updates JWT `tenant_id` (Super Admin / membership-gated on API).
 */
export async function refreshAccessToken(
  options?: RefreshAccessTokenOptions
): Promise<string | null> {
  const refreshToken = authStorage.getRefreshToken();
  const clearOnFailure = options?.clearOnFailure !== false;
  if (!refreshToken) {
    if (clearOnFailure) {
      authStorage.removeToken();
    }
    return null;
  }

  const tenantId = options?.tenantId?.trim();
  const body: { refreshToken: string; tenantId?: string } = { refreshToken };
  if (tenantId) {
    body.tenantId = tenantId;
  }

  try {
    // Raw axios — bypass response interceptor to avoid nested refresh on this call.
    // Refresh is CSRF-exempt on the API; still send token when cookie is present.
    const csrfHeaders: Record<string, string> = {
      'Content-Type': 'application/json',
      'Accept-Language': getStoredLanguage(),
    };
    const cookieToken = cookieService.getCsrfToken();
    if (cookieToken) {
      csrfHeaders[CSRF_HEADER] = cookieToken;
    }
    const refreshResponse = await axios.post<RefreshTokenResponse>(
      `${baseURL}/api/Auth/refresh`,
      body,
      {
        withCredentials: true,
        headers: csrfHeaders,
      }
    );
    const nextAccessToken = refreshResponse.data?.token;
    const nextRefreshToken = refreshResponse.data?.refreshToken;
    if (nextAccessToken) {
      authStorage.setToken(nextAccessToken);
      if (nextRefreshToken) {
        authStorage.setRefreshToken(nextRefreshToken);
      }
      return nextAccessToken;
    }
    if (clearOnFailure) {
      authStorage.removeToken();
    }
    return null;
  } catch {
    if (clearOnFailure) {
      authStorage.removeToken();
    }
    return null;
  }
}

async function performTokenRefresh(): Promise<string | null> {
  return refreshAccessToken();
}

function getOrCreateRefreshPromise(): Promise<string | null> {
  if (isDev && global._refreshPromise) {
    return global._refreshPromise;
  }
  if (!refreshPromise) {
    refreshPromise = performTokenRefresh().finally(() => {
      refreshPromise = null;
      if (isDev) {
        global._refreshPromise = null;
      }
    });
    if (isDev) {
      global._refreshPromise = refreshPromise;
    }
  }
  return refreshPromise;
}

const createAxiosInstance = () => {
  const instance = axios.create({
    baseURL: baseURL,
    // JWT via Authorization; credentials so XSRF-TOKEN cookie is stored/sent with API calls.
    withCredentials: true,
    headers: {
      'Content-Type': 'application/json',
    },
  });

  // Request Interceptor — tenant slug is read fresh from localStorage / host on every request (no instance cache).
  instance.interceptors.request.use(async (config) => {
    markApiRequestStart(config);
    if (typeof window !== 'undefined') {
      const tenantSlug = resolveTenantSlugForApiRequest();
      if (isDev && config.url) {
        technicalConsole.devDebug(
          `[API] ${TENANT_HTTP_HEADER}: ${tenantSlug || '(none)'} → ${config.url}`
        );
      }
      if (tenantSlug) {
        config.headers = config.headers ?? {};
        config.headers[TENANT_HTTP_HEADER] = tenantSlug;
        if (isDev && config.url) {
          const params = config.params ?? {};
          if (typeof params === 'object' && params !== null && !Array.isArray(params)) {
            const record = params as Record<string, unknown>;
            if (record.tenant == null) {
              config.params = { ...record, tenant: tenantSlug };
            }
          } else if (config.params == null) {
            config.params = { tenant: tenantSlug };
          }
        }
      }

      config.headers = config.headers ?? {};
      // Synced with I18nProvider via languageStorage — backend returns localized API errors.
      config.headers['Accept-Language'] = getStoredLanguage();

      const token = authStorage.getToken();
      if (token) {
        config.headers.Authorization = `Bearer ${token}`;

        if (isDev) {
          technicalConsole.devDebug(`[API] Attaching bearer token to ${config.url}`);
        }
      }

      // CSRF: skip GET/HEAD/OPTIONS; cookieService holds XSRF-TOKEN for header mirror.
      if (requestNeedsCsrf(config.method, config.url)) {
        try {
          const csrf = await ensureCsrfToken(baseURL);
          const bag: Record<string, string> = {};
          applyCsrfHeaders(bag, csrf);
          for (const [key, value] of Object.entries(bag)) {
            config.headers[key] = value;
          }
          config.withCredentials = true;
        } catch (csrfError) {
          if (isDev) {
            technicalConsole.warn('[API] CSRF token bootstrap failed', csrfError);
          }
        }
      }
    }
    // Dev-only: warn when hitting legacy endpoint prefixes (non-breaking detection).
    if (isDev && config.url && /\/api\/(Payment|Cart)\b/.test(config.url)) {
      technicalConsole.warn(
        `[API] Legacy path in use: ${config.url}. Prefer /api/admin/* or /api/pos/* when available.`
      );
    }
    return config;
  });

  // Response Interceptor
  instance.interceptors.response.use(
    (response) => {
      if (typeof window !== 'undefined') {
        sessionStorage.removeItem('regkasse_csrf_reload');
        const durationMs = takeApiRequestDurationMs(response.config);
        if (durationMs != null) {
          reportApiMetric({
            method: response.config.method,
            url: response.config.url,
            status: response.status,
            durationMs,
            ok: response.status < 400,
          });
        }
      }
      return response;
    },
    async (error) => {
      const originalRequest = error?.config;
      const status: number | undefined = error?.response?.status;
      const url = originalRequest?.url ?? error?.config?.url ?? '';
      const durationMs = takeApiRequestDurationMs(originalRequest);
      if (typeof window !== 'undefined' && durationMs != null && !isRequestCanceled(error)) {
        reportApiMetric({
          method: originalRequest?.method,
          url,
          status: status ?? 0,
          durationMs,
          ok: false,
        });
      }
      const data = error?.response?.data as
        | {
            reasonCode?: string;
            requiredPolicy?: string;
            message?: string;
            code?: string;
            error?: string;
          }
        | undefined;
      const serverMessage = typeof data?.message === 'string' ? data.message : null;
      const fallbackMessage = error?.message ?? 'Request failed';

      const suppressLogin401Noise = status === 401 && isPublicAuthEntryPath();

      // CSRF rejection: clear cache and reload once so a fresh XSRF-TOKEN is issued.
      if (status === 403 && isCsrfForbiddenMessage(serverMessage)) {
        clearCsrfTokenCache();
        if (typeof window !== 'undefined') {
          const reloadKey = 'regkasse_csrf_reload';
          const alreadyReloaded = sessionStorage.getItem(reloadKey) === '1';
          if (!alreadyReloaded) {
            sessionStorage.setItem(reloadKey, '1');
            window.location.reload();
            return Promise.reject(error);
          }
          sessionStorage.removeItem(reloadKey);
        }
      }

      if (isDev && !suppressLogin401Noise) {
        if (status === 401) {
          technicalConsole.devDebug('[API] 401 Unauthorized');
        } else if (status != null) {
          const logPayload = serverMessage ?? data ?? fallbackMessage;
          if (isExpectedLicenseWriteBlock403(status, data, serverMessage)) {
            technicalConsole.warn(`[API] HTTP ${status} ${url} (license read-only)`, logPayload);
          } else if (isOptionalSettingsNotFound404(status, url)) {
            technicalConsole.warn(`[API] HTTP ${status} ${url} (optional settings)`, logPayload);
          } else {
            technicalConsole.error(`[API] HTTP ${status} ${url}`, logPayload);
          }
        } else if (!isRequestCanceled(error)) {
          technicalConsole.warn('[API] Network or client error', {
            url: url || undefined,
            message: fallbackMessage,
            code: (error as { code?: string }).code,
          });
        } else if (isDev) {
          technicalConsole.devDebug('[API] Request cancelled', {
            url: url || undefined,
          });
        }
      }

      if (status === 403 && !isCsrfForbiddenMessage(serverMessage)) {
        const urlStr = String(url);
        const passwordChangeRequired = data?.code === 'PASSWORD_CHANGE_REQUIRED';
        const isBackupArtifactBlobDownload =
          originalRequest?.responseType === 'blob' &&
          /\/api\/admin\/backup\/runs\/[^/]+\/artifacts\/[^/]+\/download\b/.test(urlStr);
        if (!passwordChangeRequired && !isBackupArtifactBlobDownload) {
          const reasonCode =
            data?.reasonCode ?? mapRequiredPolicyToReasonCode(data?.requiredPolicy);
          const userMessage = getForbiddenMessage(reasonCode, getStoredLanguage());
          showAntdError(userMessage);
        }
      }

      if (
        status === 401 &&
        originalRequest &&
        !originalRequest._retry &&
        !String(url).includes('/api/Auth/refresh')
      ) {
        if (suppressLogin401Noise) {
          authStorage.removeToken();
        } else {
          originalRequest._retry = true;
          const nextAccessToken = await getOrCreateRefreshPromise();
          if (nextAccessToken) {
            originalRequest.headers = originalRequest.headers ?? {};
            originalRequest.headers.Authorization = `Bearer ${nextAccessToken}`;
            return instance(originalRequest);
          }
        }
      }

      // Production monitoring: report server failures only (filters 4xx / network / cancel).
      reportAxiosErrorToSentry(error);

      const normalized = {
        status: status ?? 0,
        url,
        message: serverMessage ?? fallbackMessage,
        data: data ?? null,
      };
      Object.defineProperty(error, 'normalized', { value: normalized, enumerable: false });
      return Promise.reject(error);
    }
  );

  return instance;
};

// Singleton Logic to prevent multiple instances during HMR
export const AXIOS_INSTANCE = global._axiosInstance || createAxiosInstance();

if (isDev) {
  global._axiosInstance = AXIOS_INSTANCE;
}

// Orval custom instance wrapper
export const customInstance = <T>(
  config: AxiosRequestConfig,
  options?: AxiosRequestConfig
): Promise<T> => {
  const merged = { ...config, ...options };
  const source = axios.CancelToken.source();

  const signal = merged.signal;
  let removeAbortListener: (() => void) | undefined;
  if (signal) {
    const onAbort = () => {
      source.cancel('Query was cancelled');
    };
    if (signal.aborted) {
      source.cancel('Query was cancelled');
    } else {
      signal.addEventListener?.('abort', onAbort, { once: true });
      removeAbortListener = () => signal.removeEventListener?.('abort', onAbort);
    }
  }

  const promise = AXIOS_INSTANCE({
    ...merged,
    cancelToken: source.token,
  })
    .then(({ data }) => data)
    .finally(() => {
      removeAbortListener?.();
    });

  // @ts-expect-error — orval query cancellation hook
  promise.cancel = () => {
    source.cancel('Query was cancelled');
  };

  return promise;
};
