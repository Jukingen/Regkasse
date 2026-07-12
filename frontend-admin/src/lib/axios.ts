import axios, { AxiosInstance, AxiosRequestConfig } from 'axios';
import { showAntdError } from '@/lib/antdAppBridge';
import { authStorage } from '@/features/auth/services/authStorage';
import { resolveTenantSlugForApiRequest } from '@/features/auth/services/devTenant';
import { TENANT_HTTP_HEADER } from '@/features/auth/services/tenantStorage';
import { getForbiddenMessage, mapRequiredPolicyToReasonCode } from '@/shared/errors/forbiddenMessages';
import { getStoredLanguage } from '@/i18n/languageStorage';
import { isPublicAuthEntryPath } from '@/features/auth/utils/isPublicAuthEntryPath';
import { technicalConsole } from '@/shared/dev/technicalConsole';

const isDev = process.env.NODE_ENV === 'development';

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
    serverMessage: string | null,
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

/** Shared in-flight refresh — prevents parallel `/api/Auth/refresh` (reuse detection). */
let refreshPromise: Promise<string | null> | null = null;

async function performTokenRefresh(): Promise<string | null> {
    const refreshToken = authStorage.getRefreshToken();
    if (!refreshToken) {
        authStorage.removeToken();
        return null;
    }

    try {
        // Raw axios — bypass response interceptor to avoid nested refresh on this call.
        const refreshResponse = await axios.post<RefreshTokenResponse>(
            `${baseURL}/api/Auth/refresh`,
            { refreshToken },
            {
                headers: {
                    'Content-Type': 'application/json',
                    'Accept-Language': getStoredLanguage(),
                },
            },
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
        authStorage.removeToken();
        return null;
    } catch {
        authStorage.removeToken();
        return null;
    }
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
        // JWT is sent via Authorization header (localStorage); cookies are not used for auth.
        // false avoids credentialed CORS preflight coupling; matches POS api client.
        withCredentials: false,
        headers: {
            'Content-Type': 'application/json',
        },
    });

    // Request Interceptor — tenant slug is read fresh from localStorage / host on every request (no instance cache).
    instance.interceptors.request.use((config) => {
        if (typeof window !== 'undefined') {
            const tenantSlug = resolveTenantSlugForApiRequest();
            if (isDev && config.url) {
                technicalConsole.devDebug(
                    `[API] ${TENANT_HTTP_HEADER}: ${tenantSlug || '(none)'} → ${config.url}`,
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
        }
        // Dev-only: warn when hitting legacy endpoint prefixes (non-breaking detection).
        if (isDev && config.url && /\/api\/(Payment|Cart)\b/.test(config.url)) {
            technicalConsole.warn(
                `[API] Legacy path in use: ${config.url}. Prefer /api/admin/* or /api/pos/* when available.`,
            );
        }
        return config;
    });

    // Response Interceptor
    instance.interceptors.response.use(
        (response) => {
            return response;
        },
        async (error) => {
            const originalRequest = error?.config;
            const status: number | undefined = error?.response?.status;
            const url = originalRequest?.url ?? error?.config?.url ?? '';
            const data = error?.response?.data as {
            reasonCode?: string;
            requiredPolicy?: string;
            message?: string;
            code?: string;
            error?: string;
        } | undefined;
            const serverMessage = typeof data?.message === 'string' ? data.message : null;
            const fallbackMessage = error?.message ?? 'Request failed';

            const suppressLogin401Noise = status === 401 && isPublicAuthEntryPath();

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

            if (status === 403) {
                const urlStr = String(url);
                const passwordChangeRequired = data?.code === 'PASSWORD_CHANGE_REQUIRED';
                const isBackupArtifactBlobDownload =
                    originalRequest?.responseType === 'blob' &&
                    /\/api\/admin\/backup\/runs\/[^/]+\/artifacts\/[^/]+\/download\b/.test(urlStr);
                if (!passwordChangeRequired && !isBackupArtifactBlobDownload) {
                    const reasonCode = data?.reasonCode ?? mapRequiredPolicyToReasonCode(data?.requiredPolicy);
                    const userMessage = getForbiddenMessage(reasonCode, getStoredLanguage());
                    showAntdError(userMessage);
                }
            }

            if (status === 401 && originalRequest && !originalRequest._retry && !String(url).includes('/api/Auth/refresh')) {
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

            const normalized = { status: status ?? 0, url, message: serverMessage ?? fallbackMessage, data: data ?? null };
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
export const customInstance = <T>(config: AxiosRequestConfig, options?: AxiosRequestConfig): Promise<T> => {
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

    // @ts-ignore — orval query cancellation hook
    promise.cancel = () => {
        source.cancel('Query was cancelled');
    };

    return promise;
};
