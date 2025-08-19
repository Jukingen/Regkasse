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
      // Token'ı 'Bearer ' ile sakla
      const tokenWithBearer = token.startsWith('Bearer ') ? token : `Bearer ${token}`;
      await AsyncStorage.setItem('token', tokenWithBearer);
      await AsyncStorage.setItem('tokenExpiry', Date.now().toString());
      console.log('Token stored successfully with format:', tokenWithBearer);
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
    withCredentials: false, // CORS hatası için false yapıldı
});

// Request interceptor - Token kontrolü ve ekleme
axiosInstance.interceptors.request.use(
    async (config) => {
        // 🚀 F5 REFRESH FIX: Debouncing için request tracking
        const requestKey = `${config.method}:${config.url}`;
        const currentTime = Date.now();
        const lastRequestTime = (axiosInstance as any).requestTimes?.[requestKey] || 0;
        const DEBOUNCE_MS = 500; // 500ms debounce
        
        if (currentTime - lastRequestTime < DEBOUNCE_MS) {
            console.log(`🚫 [${new Date().toISOString()}] Request debouncing: ${requestKey} - ${currentTime - lastRequestTime}ms < ${DEBOUNCE_MS}ms`);
            // Debouncing durumunda request'i iptal et
            return Promise.reject(new Error('Request debounced'));
        }
        
        // Request time'ı kaydet
        if (!(axiosInstance as any).requestTimes) {
            (axiosInstance as any).requestTimes = {};
        }
        (axiosInstance as any).requestTimes[requestKey] = currentTime;
        
        console.log('🚀 Making API request:', {
            method: config.method,
            url: config.url,
            baseURL: config.baseURL,
            fullUrl: `${config.baseURL}${config.url}`,
            headers: config.headers,
            timeout: config.timeout
        });

        try {
            // Login endpoint'inde token kontrolü yapma
            if (config.url === '/auth/login' || config.url === '/auth/register') {
                console.log('🔐 Login/Register endpoint - skipping token check');
                return config;
            }
            
            // Token'ı AsyncStorage'dan al
            const token = await AsyncStorage.getItem('token');
            console.log('🔐 Request Interceptor - Token found:', !!token, 'Token length:', token?.length);
            
            // Token varsa ve headers varsa
            if (token && config.headers) {
                // Headers'ı oluştur veya güncelle
                if (!config.headers) {
                    config.headers = new AxiosHeaders();
                }
                
                // Content-Type header'ını ayarla (eğer yoksa)
                if (!config.headers['Content-Type']) {
                    config.headers['Content-Type'] = 'application/json';
                }
                
                // Token'ı header'a ekle - token zaten 'Bearer ' ile başlıyorsa olduğu gibi kullan
                // Eğer token 'Bearer ' ile başlamıyorsa ekle
                if (token.startsWith('Bearer ')) {
                    config.headers.Authorization = token;
                } else {
                    config.headers.Authorization = `Bearer ${token}`;
                }
                
                // Debug: Token header'ı kontrol et
                console.log('🔐 Token Debug:', {
                    originalToken: token?.substring(0, 50) + '...',
                    startsWithBearer: token?.startsWith('Bearer '),
                    finalAuthHeader: config.headers.Authorization?.substring(0, 50) + '...',
                    url: config.url,
                    method: config.method
                });
                
                // Token içeriğini göster (debug için)
                try {
                    // Eğer token 'Bearer ' ile başlıyorsa kaldır
                    const cleanToken = token.startsWith('Bearer ') ? token.substring(7) : token;
                    const decoded = jwtDecode(cleanToken);
                    console.log('🔍 Token decoded:', {
                        exp: decoded.exp,
                        sub: decoded.sub,
                        currentTime: Date.now() / 1000,
                        isExpired: TokenManager.isTokenExpired(token)
                    });
                } catch (decodeError) {
                    console.error('❌ Token decode error:', decodeError);
                }
                
                // Token geçerlilik kontrolü
                if (TokenManager.isTokenExpired(token)) {
                    console.log('⚠️ Token expired, checking for refresh token...');
                    const refreshToken = await AsyncStorage.getItem('refreshToken');
                    if (refreshToken) {
                        try {
                            const response = await axios.post(`${API_BASE_URL}/auth/refresh`, {
                                refreshToken: refreshToken
                            });
                            const newToken = response.data.token;
                            await TokenManager.storeToken(newToken);
                            // Yeni token'ı header'a ekle
                            config.headers.Authorization = `Bearer ${newToken}`;
                            console.log('✅ Token refreshed successfully');
                        } catch (refreshError) {
                            console.error('❌ Token refresh failed:', refreshError);
                            await TokenManager.clearTokens();
                            // Login sayfasına yönlendirme gerekebilir
                            throw new Error('Token refresh failed');
                        }
                    } else {
                        console.log('❌ No refresh token available, token expired');
                        await TokenManager.clearTokens();
                        // Login sayfasına yönlendirme gerekebilir
                        throw new Error('Token expired and no refresh token available');
                    }
                } else {
                    console.log('✅ Valid token added to request:', {
                        hasToken: !!token,
                        tokenStart: token?.substring(0, 20) + '...',
                        authHeader: config.headers.Authorization,
                        allHeaders: JSON.stringify(config.headers)
                    });
                }
            } else {
                console.log('❌ No token found for request or no headers');
            }
            
            // Son kontrol - header'lar doğru mu?
            console.log('🔍 Final request headers:', {
                hasAuthHeader: !!config.headers?.Authorization,
                authHeader: config.headers?.Authorization,
                contentType: config.headers?.['Content-Type']
            });
        } catch (error) {
            console.error('❌ Error in request interceptor:', error);
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
        // 🚀 F5 REFRESH FIX: Debounced request'leri handle et
        if (error.message === 'Request debounced') {
            console.log('🚫 Debounced request ignored');
            return Promise.resolve(null); // Debounced request'ler için null döndür
        }
        
        console.error('❌ API error:', {
            status: error.response?.status,
            statusText: error.response?.statusText,
            url: error.config?.url,
            fullUrl: error.config?.baseURL ? `${error.config?.baseURL}${error.config?.url}` : error.config?.url,
            data: error.response?.data,
            message: error.message,
            code: error.code,
            isAxiosError: error.isAxiosError,
            timeout: error.code === 'ECONNABORTED' ? 'TIMEOUT' : 'NO_TIMEOUT',
            headers: error.config?.headers
        });

        // Request headers'ı kontrol et
        console.log('🔍 Request headers on error:', error.config?.headers);
        
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