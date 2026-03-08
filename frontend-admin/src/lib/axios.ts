import axios, { AxiosInstance, AxiosRequestConfig } from 'axios';
import { message } from 'antd';
import { authStorage } from '@/features/auth/services/authStorage';
import { getForbiddenMessage, mapRequiredPolicyToReasonCode } from '@/shared/errors/forbiddenMessages';

const isDev = process.env.NODE_ENV === 'development';
// STRICT: Require NEXT_PUBLIC_API_BASE_URL to be set, or fallback to localhost:5183 (matching backend default)
const baseURL = process.env.NEXT_PUBLIC_API_BASE_URL || 'http://localhost:5183';

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
                    console.debug(`🔌 [API] Attaching token to: ${config.url}`);
                }
            }
        }
        // Dev-only: warn when hitting legacy endpoint prefixes (non-breaking detection).
        if (isDev && config.url && /\/api\/(Payment|Cart)\b/.test(config.url)) {
            console.warn(`[API] Legacy path in use: ${config.url}. Prefer /api/admin/* or /api/pos/* when available.`);
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
                    console.debug('🔐 [API] 401 Unauthorized');
                } else if (status != null) {
                    console.error(`❌ [API] ${status} ${url}`, serverMessage ?? data ?? fallbackMessage);
                } else {
                    console.error('❌ [API] Network/client error', { url: url || undefined, message: fallbackMessage });
                }
            }

            if (status === 403) {
                const reasonCode = data?.reasonCode ?? mapRequiredPolicyToReasonCode(data?.requiredPolicy);
                const userMessage = getForbiddenMessage(reasonCode);
                message.error(userMessage);
            }

            if (status === 401 && originalRequest && !originalRequest._retry) {
                originalRequest._retry = true;
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
