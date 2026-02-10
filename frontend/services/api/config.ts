import { storage } from '../../utils/storage';
import axios from 'axios';
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
            await storage.setItem('token', cleanToken);
            await storage.setItem('tokenExpiry', Date.now().toString());
            console.log('Token stored successfully (JWT only):', cleanToken.substring(0, 20) + '...');

            // Global event: token updated (login/refresh)
            try {
                // Store last token on globalThis for non-web platforms if needed
                // eslint-disable-next-line @typescript-eslint/no-explicit-any
                (globalThis as any).__lastAuthToken__ = cleanToken;
                if (typeof window !== 'undefined' && typeof window.dispatchEvent === 'function') {
                    const evt = new CustomEvent('auth-token-updated', { detail: { token: cleanToken } });
                    window.dispatchEvent(evt);
                }
            } catch (eventError) {
                console.warn('Token update event dispatch failed:', eventError);
            }
        } catch (error) {
            console.error('Token storage failed:', error);
        }
    },

    // Token'ları temizle
    clearTokens: async () => {
        try {
            await storage.multiRemove(['token', 'refreshToken', 'tokenExpiry']);
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
        const token = await storage.getItem('token');
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
        // Logging removed or kept minimal for production noise reduction? Keeping minimal as per user request for "Net loglar"
        // console.log('✅ API response received:', response.config.url); 
        return response.data;
    },
    async (error) => {
        console.error('❌ API error:', {
            status: error.response?.status,
            url: error.config?.url,
            message: error.message
        });

        // 401 Unauthorized - Token geçersiz
        if (error.response?.status === 401) {
            console.log('⚠️ Unauthorized error (401)');

            // FIX: Check if token is actually expired locally
            const token = await storage.getItem('token');
            let isExpired = true;

            if (token) {
                isExpired = TokenManager.isTokenExpired(token);
                console.log('[API] 401 received. Local Token Expired:', isExpired);

                // Log auth header presence
                console.log('[API] Request had Auth Header:', !!error.config?.headers?.Authorization);
            }

            if (isExpired) {
                console.log('⚠️ Token expired, dispatching expiration event...');
                if (typeof window !== 'undefined' && window.dispatchEvent) {
                    const event = new CustomEvent('AUTH_SESSION_EXPIRED');
                    window.dispatchEvent(event);
                }
            } else {
                console.warn('⚠️ Server returned 401 but token is locally valid.');
                console.warn('⚠️ This might be server time skew or invalid signature.');
                // Do NOT dispatch logout event immediately if user asked to "Login'e gitme"
                // Just let the error propagate so UI can show message
            }

            // Reject the promise - letting UI handle the error state if needed before redirect happens
            return Promise.reject(error);
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
        console.log(`🌐 API POST CALL: ${url}`, { data });
        try {
            const response = await axiosInstance.post<T>(url, data, config);
            return response as T;
        } catch (error) {
            console.error('POST request failed:', error);
            throw error;
        }
    },

    put: async <T>(url: string, data?: any, config?: any): Promise<T> => {
        console.log(`🌐 API PUT CALL: ${url}`, { data });
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