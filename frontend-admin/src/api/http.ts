import axios, { AxiosInstance, AxiosRequestConfig } from 'axios';
import { authStorage } from '@/features/auth/services/authStorage';

const isDev = process.env.NODE_ENV === 'development';
const baseURL = process.env.NEXT_PUBLIC_API_BASE_URL || 'http://localhost:5000';

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
                    console.log(`🔌 [API] Header Authorization added for: ${config.url}`);
                }
            }
        }
        return config;
    });

    // Response Interceptor
    instance.interceptors.response.use(
        (response) => {
            if (isDev) {
                // Console log disabled to reduce noise, enable if needed
                // console.log(`✅ [API] Response ${response.status}:`, response.config.url);
            }
            return response;
        },
        async (error) => {
            const originalRequest = error.config;
            const status = error.response?.status;

            if (isDev) {
                if (status === 401) {
                    // Silence 401 logs on login/init
                    console.debug('🔐 [API] 401 Unauthorized (Expected for public routes)');
                } else {
                    console.error(`❌ [API] Error ${status || 'Network'}:`, {
                        url: originalRequest?.url,
                        status: status,
                    });
                }
            }

            // Handle 401 - Just reject, let app handle redirect via AuthGate
            if (status === 401 && !originalRequest._retry) {
                originalRequest._retry = true;
            }
            return Promise.reject(error);
        }
    );

    return instance;
};

// Singleton Logic
export const AXIOS_INSTANCE = global._axiosInstance || createAxiosInstance();

if (isDev) {
    global._axiosInstance = AXIOS_INSTANCE;
}

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
