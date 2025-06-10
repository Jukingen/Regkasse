import axios from 'axios';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { Platform } from 'react-native';

// Platform'a göre API URL'sini ayarla
export const API_BASE_URL = Platform.select({
    web: 'http://localhost:5183/api',
    android: 'http://10.0.2.2:5183/api',
    default: 'http://localhost:5183/api'
});

console.log('API Base URL:', API_BASE_URL); // Debug log

// Axios instance oluştur
const axiosInstance = axios.create({
    baseURL: API_BASE_URL,
    headers: {
        'Content-Type': 'application/json',
    },
});

// Request interceptor
axiosInstance.interceptors.request.use(
    async (config) => {
        console.log('Making API request:', {
            method: config.method,
            url: config.url,
            headers: config.headers
        }); // Debug log

        try {
            const token = await AsyncStorage.getItem('token');
            if (token && config.headers) {
                config.headers.Authorization = `Bearer ${token}`;
                console.log('Token added to request'); // Debug log
            } else {
                console.log('No token found for request'); // Debug log
            }
        } catch (error) {
            console.error('Error getting token:', error); // Debug log
        }
        return config;
    },
    (error) => {
        console.error('Request interceptor error:', error); // Debug log
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
        }); // Debug log
        return response.data;
    },
    async (error) => {
        console.error('API error:', {
            status: error.response?.status,
            url: error.config?.url,
            data: error.response?.data,
            message: error.message
        }); // Debug log

        if (error.response?.status === 401) {
            console.log('Unauthorized error, removing token...'); // Debug log
            try {
                await AsyncStorage.removeItem('token');
                await AsyncStorage.removeItem('refreshToken');
            } catch (e) {
                console.error('Error removing tokens:', e); // Debug log
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
    get: async <T>(url: string, config?: any) => {
        console.log('GET request:', { url, config }); // Debug log
        try {
            return await axiosInstance.get<T>(url, config);
        } catch (error) {
            console.error('GET request failed:', error); // Debug log
            throw error;
        }
    },

    post: async <T>(url: string, data?: any, config?: any) => {
        console.log('POST request:', { url, data, config }); // Debug log
        try {
            return await axiosInstance.post<T>(url, data, config);
        } catch (error) {
            console.error('POST request failed:', error); // Debug log
            throw error;
        }
    },

    put: async <T>(url: string, data?: any, config?: any) => {
        console.log('PUT request:', { url, data, config }); // Debug log
        try {
            return await axiosInstance.put<T>(url, data, config);
        } catch (error) {
            console.error('PUT request failed:', error); // Debug log
            throw error;
        }
    },

    delete: async <T>(url: string, config?: any) => {
        console.log('DELETE request:', { url, config }); // Debug log
        try {
            return await axiosInstance.delete<T>(url, config);
        } catch (error) {
            console.error('DELETE request failed:', error); // Debug log
            throw error;
        }
    }
}; 