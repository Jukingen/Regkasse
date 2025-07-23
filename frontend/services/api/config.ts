import AsyncStorage from '@react-native-async-storage/async-storage';
import axios from 'axios';

// API base URL - Expo ve React Native Web için uyumlu
// .env desteği yoksa sabit fallback kullanılır
export const API_BASE_URL =
  (typeof process !== 'undefined' && process.env.API_BASE_URL) ||
  'http://localhost:5183';

console.log('API Base URL:', API_BASE_URL);

// Axios instance oluştur
const axiosInstance = axios.create({
    baseURL: API_BASE_URL,
    headers: {
        'Content-Type': 'application/json',
    },
    timeout: 10000, // 10 saniye timeout
});

// Request interceptor
axiosInstance.interceptors.request.use(
    async (config) => {
        console.log('Making API request:', {
            method: config.method,
            url: config.url,
            baseURL: config.baseURL,
            headers: config.headers
        });

        try {
            const token = await AsyncStorage.getItem('token');
            if (token && config.headers) {
                config.headers.Authorization = `Bearer ${token}`;
                console.log('Token added to request');
            } else {
                console.log('No token found for request');
            }
        } catch (error) {
            console.error('Error getting token:', error);
        }
        return config;
    },
    (error) => {
        console.error('Request interceptor error:', error);
        return Promise.reject(error);
    }
);

// Response interceptor
axiosInstance.interceptors.response.use(
    (response) => {
        console.log('API response received:', {
            status: response.status,
            url: response.config.url,
            data: response.data
        });
        return response.data;
    },
    async (error) => {
        console.error('API error:', {
            status: error.response?.status,
            url: error.config?.url,
            data: error.response?.data,
            message: error.message
        });

        if (error.response?.status === 401) {
            console.log('Unauthorized error, removing token...');
            try {
                await AsyncStorage.removeItem('token');
                await AsyncStorage.removeItem('refreshToken');
            } catch (e) {
                console.error('Error removing tokens:', e);
            }
        }

        // Hata detaylarını döndür
        throw {
            status: error.response?.status,
            data: error.response?.data,
            message: error.message
        };
    }
);

// API client
export const apiClient = {
    get: async <T>(url: string, config?: any): Promise<T> => {
        console.log('GET request:', { url, config });
        try {
            const response = await axiosInstance.get<T>(url, config);
            return response as T;
        } catch (error) {
            console.error('GET request failed:', error);
            throw error;
        }
    },

    post: async <T>(url: string, data?: any, config?: any): Promise<T> => {
        console.log('POST request:', { url, data, config });
        try {
            const response = await axiosInstance.post<T>(url, data, config);
            return response as T;
        } catch (error) {
            console.error('POST request failed:', error);
            throw error;
        }
    },

    put: async <T>(url: string, data?: any, config?: any): Promise<T> => {
        console.log('PUT request:', { url, data, config });
        try {
            const response = await axiosInstance.put<T>(url, data, config);
            return response as T;
        } catch (error) {
            console.error('PUT request failed:', error);
            throw error;
        }
    },

    delete: async <T>(url: string, config?: any): Promise<T> => {
        console.log('DELETE request:', { url, config });
        try {
            const response = await axiosInstance.delete<T>(url, config);
            return response as T;
        } catch (error) {
            console.error('DELETE request failed:', error);
            throw error;
        }
    }
}; 