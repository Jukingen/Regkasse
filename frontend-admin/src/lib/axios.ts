import axios, { AxiosInstance, AxiosRequestConfig } from 'axios';
import { message } from 'antd';
import { authStorage } from '@/features/auth/services/authStorage';
import { getForbiddenMessage, mapRequiredPolicyToReasonCode } from '@/shared/errors/forbiddenMessages';
import { getStoredLanguage } from '@/i18n/languageStorage';
import { technicalConsole } from '@/shared/dev/technicalConsole';

const isDev = process.env.NODE_ENV === 'development';
const configuredBaseUrl = process.env.NEXT_PUBLIC_API_BASE_URL;
const baseURL = configuredBaseUrl || (isDev ? 'http://localhost:5183' : '');

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
        withCredentials: true,
        headers: {
            'Content-Type': 'application/json',
        },
    });

    // Request Interceptor
    instance.interceptors.request.use((config) => {
        if (typeof window !== 'undefined') {
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

            if (isDev) {
                if (status === 401) {
                    technicalConsole.devDebug('[API] 401 Unauthorized');
                } else if (status != null) {
                    technicalConsole.error(`[API] HTTP ${status} ${url}`, serverMessage ?? data ?? fallbackMessage);
                } else {
                    technicalConsole.error('[API] Network or client error', {
                        url: url || undefined,
                        message: fallbackMessage,
                    });
                }
            }

            if (status === 403) {
                const reasonCode = data?.reasonCode ?? mapRequiredPolicyToReasonCode(data?.requiredPolicy);
                const userMessage = getForbiddenMessage(reasonCode, getStoredLanguage());
                message.error(userMessage);
            }

            if (status === 401 && originalRequest && !originalRequest._retry && !String(url).includes('/api/Auth/refresh')) {
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
                    } catch {
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
