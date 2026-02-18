import axios, { AxiosInstance, AxiosRequestConfig } from 'axios';
import { authStorage } from '@/features/auth/services/authStorage';

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
        return config;
    });

    // Response Interceptor
    instance.interceptors.response.use(
        (response) => {
            return response;
        },
        async (error) => {
            const originalRequest = error.config;
            const status = error.response?.status;

            if (isDev) {
                if (status === 401) {
                    console.debug('🔐 [API] 401 Unauthorized (Expected for public routes or expired token)');
                } else {
                    console.error(`❌ [API] Error ${status}:`, {
                        url: originalRequest?.url,
                        data: error.response?.data,
                    });
                }
            }

            // Handle 401 - You might want to dispatch a specific event here or just let React Query handle it (retry: false)
            if (status === 401 && !originalRequest._retry) {
                originalRequest._retry = true;
                // Potential TODO: Refresh token logic here if implemented
            }

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
