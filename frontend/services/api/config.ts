import AsyncStorage from '@react-native-async-storage/async-storage';
import axios from 'axios';
import { jwtDecode } from 'jwt-decode';

// API base URL - Expo ve React Native Web için uyumlu
// .env desteği yoksa sabit fallback kullanılır
export const API_BASE_URL =
  (typeof process !== 'undefined' && process.env.EXPO_PUBLIC_API_URL) ||
  'http://localhost:5183/api';

console.log('API Base URL:', API_BASE_URL);

// Token yönetimi için yardımcı fonksiyonlar
const TokenManager = {
  // Token'ın geçerlilik süresini kontrol et
  isTokenExpired: (token: string): boolean => {
    try {
      const decoded = jwtDecode(token) as any;
      const currentTime = Date.now() / 1000;
      return decoded.exp ? decoded.exp < currentTime : true;
    } catch (error) {
      console.error('Token expiration check failed:', error);
      return true;
    }
  },

  // Token'dan kullanıcı bilgilerini çıkar
  getTokenInfo: (token: string) => {
    try {
      return jwtDecode(token) as any;
    } catch (error) {
      console.error('Token decode failed:', error);
      return null;
    }
  },

  // Güvenli token saklama
  storeToken: async (token: string, refreshToken: string) => {
    try {
      await AsyncStorage.setItem('token', token);
      await AsyncStorage.setItem('refreshToken', refreshToken);
      await AsyncStorage.setItem('tokenExpiry', Date.now().toString());
    } catch (error) {
      console.error('Token storage failed:', error);
    }
  },

  // Token'ları temizle
  clearTokens: async () => {
    try {
      await AsyncStorage.multiRemove(['token', 'refreshToken', 'tokenExpiry']);
    } catch (error) {
      console.error('Token cleanup failed:', error);
    }
  }
};

// Axios instance oluştur
const axiosInstance = axios.create({
    baseURL: API_BASE_URL,
    headers: {
        'Content-Type': 'application/json',
    },
    timeout: 10000, // 10 saniye timeout
});

// Request interceptor - Token kontrolü ve ekleme
axiosInstance.interceptors.request.use(
    async (config) => {
        console.log('🚀 Making API request:', {
            method: config.method,
            url: config.url,
            baseURL: config.baseURL,
            fullUrl: `${config.baseURL}${config.url}`,
            headers: config.headers,
            timeout: config.timeout
        });

        try {
            const token = await AsyncStorage.getItem('token');
            if (token && config.headers) {
                // Token geçerlilik kontrolü
                if (TokenManager.isTokenExpired(token)) {
                    console.log('Token expired, attempting refresh...');
                    const refreshToken = await AsyncStorage.getItem('refreshToken');
                    if (refreshToken) {
                        try {
                            const response = await axios.post(`${API_BASE_URL}/auth/refresh`, {
                                refreshToken: refreshToken
                            });
                            const newToken = response.data.token;
                            await TokenManager.storeToken(newToken, refreshToken);
                            config.headers.Authorization = `Bearer ${newToken}`;
                            console.log('Token refreshed successfully');
                        } catch (refreshError) {
                            console.error('Token refresh failed:', refreshError);
                            await TokenManager.clearTokens();
                            // Login sayfasına yönlendirme gerekebilir
                            throw new Error('Token refresh failed');
                        }
                    } else {
                        console.log('No refresh token found');
                        await TokenManager.clearTokens();
                    }
                } else {
                    config.headers.Authorization = `Bearer ${token}`;
                    console.log('Valid token added to request');
                }
            } else {
                console.log('No token found for request');
            }
        } catch (error) {
            console.error('Error in request interceptor:', error);
        }
        return config;
    },
    (error) => {
        console.error('Request interceptor error:', error);
        return Promise.reject(error);
    }
);

// Response interceptor - Hata yönetimi ve token yenileme
axiosInstance.interceptors.response.use(
    (response) => {
        console.log('✅ API response received:', {
            status: response.status,
            url: response.config.url,
            fullUrl: `${response.config.baseURL}${response.config.url}`,
            data: response.data,
            headers: response.headers
        });
        return response.data;
    },
    async (error) => {
        console.error('❌ API error:', {
            status: error.response?.status,
            url: error.config?.url,
            fullUrl: error.config?.baseURL ? `${error.config.baseURL}${error.config.url}` : error.config?.url,
            data: error.response?.data,
            message: error.message,
            code: error.code,
            isAxiosError: error.isAxiosError,
            timeout: error.code === 'ECONNABORTED' ? 'TIMEOUT' : 'NO_TIMEOUT'
        });

        // 401 Unauthorized - Token geçersiz
        if (error.response?.status === 401) {
            console.log('Unauthorized error, attempting token refresh...');
            try {
                const refreshToken = await AsyncStorage.getItem('refreshToken');
                if (refreshToken) {
                    const response = await axios.post(`${API_BASE_URL}/auth/refresh`, {
                        refreshToken: refreshToken
                    });
                    const newToken = response.data.token;
                    await TokenManager.storeToken(newToken, refreshToken);
                    
                    // Orijinal isteği yeni token ile tekrarla
                    const originalRequest = error.config;
                    originalRequest.headers.Authorization = `Bearer ${newToken}`;
                    return axiosInstance(originalRequest);
                } else {
                    console.log('No refresh token available');
                    await TokenManager.clearTokens();
                }
            } catch (refreshError) {
                console.error('Token refresh failed in response interceptor:', refreshError);
                await TokenManager.clearTokens();
            }
        }

        // 403 Forbidden - Yetkisiz erişim
        if (error.response?.status === 403) {
            console.log('Forbidden error - insufficient permissions');
            // Kullanıcıya yetkisiz erişim mesajı gösterilebilir
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

// Token yönetimi için export
export { TokenManager }; 