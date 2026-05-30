import axios, { AxiosInstance, AxiosRequestConfig } from 'axios';
import { message } from 'antd';
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
    const message = (error as { message?: string })?.message;
    if (message === 'canceled' || message === 'Query was cancelled') {
        return true;
    }
    return (error as { code?: string })?.code === 'ERR_CANCELED';
}

const configuredBaseUrl = process.env.NEXT_PUBLIC_API_BASE_URL;
const baseURL = configuredBaseUrl || (isDev ? 'http://localhost:5184' : '');

if (!baseURL) {
    throw new Error('NEXT_PUBLIC_API_BASE_URL must be configured for non-development environments.');
}

// Extended window interface for global singleton in Dev
declare global {
    var _axiosInstance: AxiosInstance | undefined;
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
            const data = error?.response?.data as { reasonCode?: string; requiredPolicy?: string; message?: string } | undefined;
            const serverMessage = typeof data?.message === 'string' ? data.message : null;
            const fallbackMessage = error?.message ?? 'Request failed';

            const suppressLogin401Noise = status === 401 && isPublicAuthEntryPath();

            if (isDev && !suppressLogin401Noise) {
                if (status === 401) {
                    technicalConsole.devDebug('[API] 401 Unauthorized');
                } else if (status != null) {
                    technicalConsole.error(`[API] HTTP ${status} ${url}`, serverMessage ?? data ?? fallbackMessage);
                } else if (!isRequestCanceled(error)) {
                    technicalConsole.error('[API] Network or client error', {
                        url: url || undefined,
                        message: fallbackMessage,
                    });
                }
            }

            if (status === 403) {
                const urlStr = String(url);
                const isBackupArtifactBlobDownload =
                    originalRequest?.responseType === 'blob' &&
                    /\/api\/admin\/backup\/runs\/[^/]+\/artifacts\/[^/]+\/download\b/.test(urlStr);
                if (!isBackupArtifactBlobDownload) {
                    const reasonCode = data?.reasonCode ?? mapRequiredPolicyToReasonCode(data?.requiredPolicy);
                    const userMessage = getForbiddenMessage(reasonCode, getStoredLanguage());
                    message.error(userMessage);
                }
            }

            if (status === 401 && originalRequest && !originalRequest._retry && !String(url).includes('/api/Auth/refresh')) {
                if (suppressLogin401Noise) {
                    authStorage.removeToken();
                } else {
                    originalRequest._retry = true;
                    const refreshToken = authStorage.getRefreshToken();
                    if (refreshToken) {
                        try {
                            const refreshResponse = await instance.post('/api/Auth/refresh', { refreshToken });
                            const nextAccessToken = (refreshResponse.data as any)?.token as string | undefined;
                            const nextRefreshToken = (refreshResponse.data as any)?.refreshToken as string | undefined;
                            if (nextAccessToken) {
                                authStorage.setToken(nextAccessToken);
                                if (nextRefreshToken) {
                                    authStorage.setRefreshToken(nextRefreshToken);
                                }
                                originalRequest.headers = originalRequest.headers ?? {};
                                originalRequest.headers.Authorization = `Bearer ${nextAccessToken}`;
                                return instance(originalRequest);
                            }
                            authStorage.removeToken();
                        } catch {
                            authStorage.removeToken();
                        }
                    } else {
                        authStorage.removeToken();
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
    const source = axios.CancelToken.source();
    const promise = AXIOS_INSTANCE({
        ...config,
        ...options,
        cancelToken: source.token,
    }).then(({ data }) => data);

    // @ts-ignore
    promise.cancel = () => {
        source.cancel('Query was cancelled');
    };

    return promise;
};
