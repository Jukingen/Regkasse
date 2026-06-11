import axios, { type InternalAxiosRequestConfig } from 'axios';
import { jwtDecode } from 'jwt-decode';
import { sessionManager } from '../session/sessionManager';
import {
  applyTenantHeader,
  resolveEffectiveTenantSlug,
} from '../tenant/devTenant';
import { tenantStorage, TENANT_HTTP_HEADER } from '../tenant/tenantStorage';

import {
  API_BASE_URL as CONFIGURED_API_BASE_URL,
  stripDevTenantQueryFromBaseUrl,
} from '../../config';
import {
  applyDevNetworkDelayIfConfigured,
} from '../../src/config/devFlags';
const isDev = __DEV__;

// Platform-aware API URL from main config
export const API_BASE_URL = CONFIGURED_API_BASE_URL;

/** Applies tenant-specific API base URL after license activation (no-op when unchanged). */
export function applyStoredApiBaseUrl(url: string): void {
    const normalized = stripDevTenantQueryFromBaseUrl(url);
    if (!normalized || axiosInstance.defaults.baseURL === normalized) {
        return;
    }
    axiosInstance.defaults.baseURL = normalized;
    if (isDev) {
        console.log('🔧 API base URL updated from tenant bootstrap:', normalized);
    }
}

/** Restores configured default API base URL (e.g. after logout). */
export function resetApiBaseUrlToConfigured(): void {
    if (axiosInstance.defaults.baseURL !== CONFIGURED_API_BASE_URL) {
        axiosInstance.defaults.baseURL = CONFIGURED_API_BASE_URL;
    }
}

/**
 * Ensures axios base URL has no <c>?tenant=</c> suffix (dev tenant uses {@link TENANT_HTTP_HEADER} per request).
 * Call after dev tenant switch or on auth bootstrap to clear legacy mis-built base URLs.
 */
export async function hydrateDevTenantApiBaseUrl(): Promise<void> {
    if (!isDev) return;
    const next = stripDevTenantQueryFromBaseUrl(CONFIGURED_API_BASE_URL);
    if (axiosInstance.defaults.baseURL !== next) {
        axiosInstance.defaults.baseURL = next;
        if (isDev) {
            console.log('🔧 API base URL normalized (tenant via X-Tenant-Id):', next);
        }
    }
}

/** Resolves slug from login/license persistence, with dev override when applicable. */
async function resolveRequestTenantSlug(): Promise<string | null> {
    const persistedSlug = await tenantStorage.getTenantSlug();
    return resolveEffectiveTenantSlug(persistedSlug);
}

/** Adds {@link TENANT_HTTP_HEADER} when a tenant slug is available. */
export async function addTenantHeader(
    config: InternalAxiosRequestConfig,
): Promise<InternalAxiosRequestConfig> {
    const tenantSlug = await resolveRequestTenantSlug();
    if (!tenantSlug) {
        return config;
    }

    config.headers = applyTenantHeader(
        config.headers as Record<string, unknown> | undefined,
        tenantSlug,
    ) as typeof config.headers;

    return config;
}

/** Headers for raw <c>fetch()</c> (axios request interceptor adds tenant header automatically). */
export async function resolveTenantFetchHeaders(
    headers: Record<string, string> = {},
): Promise<Record<string, string>> {
    const tenantSlug = await resolveRequestTenantSlug();
    if (!tenantSlug) return headers;
    return applyTenantHeader(headers, tenantSlug) as Record<string, string>;
}

if (isDev) {
    console.log('🔧 API Services - Using API Base URL:', API_BASE_URL);
}

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
            const cleanToken = sessionManager.normalizeToken(token);
            await sessionManager.persistSession({ token: cleanToken });
            if (isDev) {
                console.log('Token stored successfully.');
            }

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
            if (isDev) {
                console.error('Token storage failed:', error);
            }
        }
    },

    // Token'ları temizle
    clearTokens: async () => {
        try {
            await sessionManager.clearSession();
        } catch (error) {
            if (isDev) {
                console.error('Token cleanup failed:', error);
            }
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

const REFRESH_HEADER = 'x-auth-refresh-retry';

/** Map known backend Turkish literals to German for POS UI (backend unchanged). */
function translateKnownTurkishApiErrorMessages(data: unknown): void {
    if (!data || typeof data !== 'object' || Array.isArray(data)) return;
    const body = data as Record<string, unknown>;
    const msg = body.message;
    if (typeof msg !== 'string') return;

    const turkishToGerman: Array<[string, string]> = [
        ['Kullanıcı bulunamadı', 'Benutzername oder E-Mail nicht gefunden.'],
        ['Geçersiz şifre', 'Falsches Passwort. Bitte versuchen Sie es erneut.'],
        ['Hesap aktif değil', 'Ihr Konto ist gesperrt. Bitte kontaktieren Sie Ihren Administrator.'],
        [
            'Bu kullanıcı bu uygulama için yetkili değil.',
            'Sie haben keine Berechtigung für die POS-App. Bitte kontaktieren Sie Ihren Administrator.',
        ],
    ];

    for (const [turkish, german] of turkishToGerman) {
        if (msg.includes(turkish)) {
            body.message = msg.split(turkish).join(german);
            return;
        }
    }
}

// Request interceptor - Token kontrolü ve ekleme
axiosInstance.interceptors.request.use(
    async (config) => {
        await applyDevNetworkDelayIfConfigured();

        await addTenantHeader(config);

        // Token kontrolü
        const token = await sessionManager.getAccessToken();
        if (token) {
            // Token'ın geçerlilik süresini kontrol et
            if (TokenManager.isTokenExpired(token)) {
                if (isDev) {
                    console.log('Token expired, clearing...');
                }
                await TokenManager.clearTokens();
                // router.replace('/login'); // router kaldırıldığı için bu satır kaldırıldı
                return Promise.reject(new Error('Token expired'));
            }

            // Token'ı header'a ekle (JWT token'a Bearer prefix ekle)
            config.headers.Authorization = `Bearer ${token}`;
        }
        return config;
    },
    (error) => {
        if (isDev) {
            console.error('❌ Request interceptor error:', error);
        }
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
        if (error.response?.data) {
            translateKnownTurkishApiErrorMessages(error.response.data);
        }

        if (isDev) {
            console.error('❌ API error:', {
                status: error.response?.status,
                url: error.config?.url,
                message: error.message
            });
        }

        // 401 Unauthorized - Token geçersiz
        if (error.response?.status === 401) {
            if (isDev) {
                console.log('⚠️ Unauthorized error (401)');
            }

            // FIX: Check if token is actually expired locally
            const token = await sessionManager.getAccessToken();
            let isExpired = true;

            if (token) {
                isExpired = TokenManager.isTokenExpired(token);
                if (isDev) {
                    console.log('[API] 401 received. Local Token Expired:', isExpired);
                }

                // Log auth header presence
                if (isDev) {
                    console.log('[API] Request had Auth Header:', !!error.config?.headers?.Authorization);
                }
            }

            const originalConfig = error.config || {};
            const wasRetried = !!originalConfig.headers?.[REFRESH_HEADER];

            if (!wasRetried) {
                const refreshedToken = await sessionManager.refreshAccessToken((refreshToken) =>
                    axiosInstance.post<{ token: string }>(
                        '/auth/refresh',
                        { refreshToken },
                        { headers: { [REFRESH_HEADER]: '1' } }
                    )
                );

                if (refreshedToken) {
                    originalConfig.headers = {
                        ...(originalConfig.headers || {}),
                        Authorization: `Bearer ${refreshedToken}`,
                        [REFRESH_HEADER]: '1',
                    };
                    return axiosInstance(originalConfig);
                }
            }

            if (isExpired || wasRetried) {
                if (isDev) {
                    console.log('⚠️ Session invalid/refresh failed, dispatching expiration event...');
                }
                if (typeof window !== 'undefined' && window.dispatchEvent) {
                    const event = new CustomEvent('AUTH_SESSION_EXPIRED');
                    window.dispatchEvent(event);
                }
            } else {
                if (isDev) {
                    console.warn('⚠️ Server returned 401 but token is locally valid.');
                    console.warn('⚠️ This might be server time skew or invalid signature.');
                }
                // Do NOT dispatch logout event immediately if user asked to "Login'e gitme"
                // Just let the error propagate so UI can show message
            }

            // Reject the promise - letting UI handle the error state if needed before redirect happens
            return Promise.reject(error);
        }

        // 403 Forbidden - Yetkisiz erişim
        if (error.response?.status === 403) {
            if (isDev) {
                console.log('⛔ Forbidden error (403) - insufficient permissions');
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
        try {
            const response = await axiosInstance.get<T>(url, config);
            return response as T;
        } catch (error) {
            if (isDev) {
                console.error('GET request failed:', error);
            }
            throw error;
        }
    },

    post: async <T>(url: string, data?: any, config?: any): Promise<T> => {
        try {
            const response = await axiosInstance.post<T>(url, data, config);
            return response as T;
        } catch (error) {
            if (isDev) {
                console.error('POST request failed:', error);
            }
            throw error;
        }
    },

    put: async <T>(url: string, data?: any, config?: any): Promise<T> => {
        try {
            const response = await axiosInstance.put<T>(url, data, config);
            return response as T;
        } catch (error) {
            if (isDev) {
                console.error('PUT request failed:', error);
            }
            throw error;
        }
    },

    delete: async <T>(url: string, config?: any): Promise<T> => {
        try {
            const response = await axiosInstance.delete<T>(url, config);
            return response as T;
        } catch (error) {
            if (isDev) {
                console.error('DELETE request failed:', error);
            }
            throw error;
        }
    }
};

// Token yönetimi için export
export { TokenManager }; 