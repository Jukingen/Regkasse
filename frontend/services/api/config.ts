import AsyncStorage from '@react-native-async-storage/async-storage';
import axios, { AxiosHeaders } from 'axios';
import { jwtDecode } from 'jwt-decode';

import { API_BASE_URL as CONFIGURED_API_BASE_URL } from '../../config';

// Platform-aware API URL from main config
export const API_BASE_URL = CONFIGURED_API_BASE_URL;

console.log('🔧 API Services - Using API Base URL:', API_BASE_URL);

// Token yönetimi için yardımcı fonksiyonlar
const TokenManager = {
  // Token'ın geçerlilik süresini kontrol et
  isTokenExpired: (token: string): boolean => {
    try {
      // Eğer token 'Bearer ' ile başlıyorsa kaldır
      const cleanToken = token.startsWith('Bearer ') ? token.substring(7) : token;
      
      const decoded = jwtDecode(cleanToken) as any;
      const currentTime = Date.now() / 1000;
      const isExpired = decoded.exp ? decoded.exp < currentTime : true;
      
      console.log('Token expiration check:', {
        exp: decoded.exp,
        currentTime: currentTime,
        isExpired: isExpired,
        timeLeft: decoded.exp ? Math.round((decoded.exp - currentTime) / 60) + ' minutes' : 'unknown'
      });
      
      return isExpired;
    } catch (error) {
      console.error('Token expiration check failed:', error);
      return true;
    }
  },

  // Token'dan kullanıcı bilgilerini çıkar
  getTokenInfo: (token: string) => {
    try {
      // Eğer token 'Bearer ' ile başlıyorsa kaldır
      const cleanToken = token.startsWith('Bearer ') ? token.substring(7) : token;
      return jwtDecode(cleanToken) as any;
    } catch (error) {
      console.error('Token decode failed:', error);
      return null;
    }
  },

  // Güvenli token saklama
  storeToken: async (token: string) => {
    try {
      // Token'ı 'Bearer ' prefix olmadan sakla (sadece JWT token)
      const cleanToken = token.startsWith('Bearer ') ? token.substring(7) : token;
      await AsyncStorage.setItem('token', cleanToken);
      await AsyncStorage.setItem('tokenExpiry', Date.now().toString());
      console.log('Token stored successfully (JWT only):', cleanToken.substring(0, 20) + '...');
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
    timeout: 30000, // ✅ YENİ: 30 saniye timeout - RKSV işlemleri için optimize edildi
    withCredentials: false, // CORS hatası için false yapıldı
});

// Request interceptor - Token kontrolü ve ekleme
axiosInstance.interceptors.request.use(
    async (config) => {
        // 🚀 DEBOUNCING KALDIRILDI - Ürün yükleme için basitleştirildi
        
        // Token kontrolü
        const token = await AsyncStorage.getItem('token');
        if (token) {
            // Token'ın geçerlilik süresini kontrol et
            if (TokenManager.isTokenExpired(token)) {
                console.log('Token expired, clearing...');
                await TokenManager.clearTokens();
                // router.replace('/login'); // router kaldırıldığı için bu satır kaldırıldı
                return Promise.reject(new Error('Token expired'));
            }
            
            // Token'ı header'a ekle (JWT token'a Bearer prefix ekle)
            config.headers.Authorization = `Bearer ${token}`;
            console.log('✅ Token added to request:', config.url, 'Token length:', token.length);
        } else {
            console.log('⚠️ No token found, proceeding without auth');
        }
        
        console.log(`🚀 [${new Date().toISOString()}] Request: ${config.method?.toUpperCase()} ${config.url}`);
        return config;
    },
    (error) => {
        console.error('❌ Request interceptor error:', error);
        return Promise.reject(error);
    }
);

// Response interceptor - Hata yönetimi ve token yenileme
axiosInstance.interceptors.response.use(
    (response) => {
        console.log('✅ API response received:', {
            status: response.status,
            url: response.config.url,
            data: response.data
        });
        return response.data;
    },
    async (error) => {
        console.error('❌ API error:', {
            status: error.response?.status,
            statusText: error.response?.statusText,
            url: error.config?.url,
            data: error.response?.data,
            message: error.message,
            code: error.code
        });

        // 401 Unauthorized - Token geçersiz
        if (error.response?.status === 401) {
            console.log('⚠️ Unauthorized error (401), checking token status...');
            
            // Token'ı kontrol et
            const token = await AsyncStorage.getItem('token');
            console.log('🔐 Current token status:', {
                exists: !!token,
                length: token?.length,
                isExpired: token ? TokenManager.isTokenExpired(token) : true
            });
            
            try {
                const refreshToken = await AsyncStorage.getItem('refreshToken');
                console.log('🔄 Refresh token status:', {
                    exists: !!refreshToken,
                    length: refreshToken?.length
                });
                
                if (refreshToken) {
                    console.log('🔄 Attempting token refresh...');
                    try {
                        const response = await axios.post(`${API_BASE_URL}/auth/refresh`, {
                            refreshToken: refreshToken
                        });
                        const newToken = response.data.token;
                        await TokenManager.storeToken(newToken);
                        console.log('✅ Token refreshed successfully, retrying request');
                        
                        // Orijinal isteği yeni token ile tekrarla
                        const originalRequest = error.config;
                        // newToken zaten JWT token, Bearer prefix ekle
                        originalRequest.headers.Authorization = `Bearer ${newToken}`;
                        return axiosInstance(originalRequest);
                    } catch (refreshError) {
                        console.error('❌ Token refresh failed:', refreshError);
                        await TokenManager.clearTokens();
                    }
                } else {
                    console.log('❌ No refresh token available for 401 error');
                    await TokenManager.clearTokens();
                }
            } catch (refreshError) {
                console.error('❌ Token refresh process failed:', refreshError);
                await TokenManager.clearTokens();
            }
        }

        // 403 Forbidden - Yetkisiz erişim
        if (error.response?.status === 403) {
            console.log('⛔ Forbidden error (403) - insufficient permissions');
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