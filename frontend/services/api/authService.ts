import axios from 'axios';
import { apiClient } from './config';
import { normalizeLoginError } from '../../features/auth/authErrors';
import { sessionManager } from '../session/sessionManager';
import { buildLoginPayload, type LoginRequest } from './loginPayload';

export { buildLoginPayload, type LoginRequest } from './loginPayload';
const isDev = __DEV__;

function getHttpStatusFromError(error: unknown): number | undefined {
  if (axios.isAxiosError(error)) {
    return error.response?.status;
  }
  const legacy = error as { status?: number; response?: { status?: number } };
  return legacy.response?.status ?? legacy.status;
}

export interface LoginResponse {
  token: string;
  expiresIn?: number;
  refreshToken?: string; // Opsiyonel, backend'den gelirse kullan
  user: {
    id: string;
    username?: string;
    email: string;
    role: string;
    firstName?: string;
    lastName?: string;
    roles?: string[];
    permissions?: string[];
    tenantId?: string | null;
    tenantSlug?: string | null;
    tenantDisplayName?: string | null;
    mustChangePasswordOnNextLogin?: boolean;
  };
}

export interface User {
  id: string;
  username?: string;
  email: string;
  role: string;
  firstName?: string;
  lastName?: string;
  roles?: string[];
  /** Backend permission claims (resource.action). Aligned with backend RolePermissionMatrix. */
  permissions?: string[];
  mustChangePasswordOnNextLogin?: boolean;
}

// Login işlemi
export const login = async (credentials: LoginRequest): Promise<LoginResponse> => {
  try {
    const response = await apiClient.post<LoginResponse>('/auth/login', credentials);

    // Token storage'ı AuthContext'te yapılıyor, burada yapmıyoruz
    // await storage.setItem('token', response.token);
    // await storage.setItem('user', JSON.stringify(response.user));

    return response;
  } catch (error: unknown) {
    if (isDev) {
      console.error('Login error:', error);
    }
    // Propagate original error (response/data/message preserved via normalizeLoginError); no generic overwrite.
    throw normalizeLoginError(error);
  }
};

export const logout = async (): Promise<void> => {
  try {
    await apiClient.post('/auth/logout');
  } catch (error) {
    if (isDev) {
      console.warn('Backend logout call failed (non-critical):', error);
    }
  } finally {
    await sessionManager.clearSession();
  }
};

// Mevcut kullanıcıyı getir
export const getCurrentUser = async (): Promise<User | null> => {
  try {
    const user = await sessionManager.getStoredUser();
    if (!user) {
      if (isDev) {
        console.log('No user data found in storage');
      }
      return null;
    }
    if (isDev) {
        console.log('Retrieved user from storage');
    }
    return user;
  } catch (error) {
    if (isDev) {
      console.error('Get current user error:', error);
    }
    return null;
  }
};

// 🔐 BACKEND TOKEN VALIDATION - F5 refresh'te backend'den kullanıcı durumunu kontrol eder
export const validateToken = async (): Promise<User | null> => {
  // 🚀 F5 REFRESH FIX: Debouncing için static flag
  if ((validateToken as any).isValidating) {
    if (isDev) {
      console.log('🚫 validateToken zaten çalışıyor, atlanıyor...');
    }
    return null;
  }

  // 🚀 F5 REFRESH FIX: Debouncing için timestamp kontrol
  const currentTime = Date.now();
  const lastCallTime = (validateToken as any).lastCallTime || 0;
  const DEBOUNCE_MS = 1000; // 1 saniye debounce

  if (currentTime - lastCallTime < DEBOUNCE_MS) {
    if (isDev) {
      console.log(`🚫 validateToken debouncing: ${currentTime - lastCallTime}ms < ${DEBOUNCE_MS}ms`);
    }
    return null;
  }

  // Flag'leri set et
  (validateToken as any).isValidating = true;
  (validateToken as any).lastCallTime = currentTime;

  try {
    if (isDev) {
      console.log('🔐 Backend token validation başlatılıyor...');
    }

    // Token'ı AsyncStorage'dan al
    const token = await sessionManager.getAccessToken();
    if (!token) {
      if (isDev) {
        console.log('❌ Token bulunamadı, validation atlanıyor');
      }
      return null;
    }

    if (isDev) {
      console.log('🔑 Token bulundu, backend\'e gönderiliyor...');
    }

    // Backend'den kullanıcı bilgisini al
    const response = await apiClient.get<User>('/auth/me');

    if (response && response.id) {
      if (isDev) {
        console.log('✅ Backend token validation başarılı:', response.id);
      }

      // AsyncStorage'daki user bilgisini güncelle
      await sessionManager.persistSession({ token, user: response });

      return response;
    } else {
      if (isDev) {
        console.log('❌ Backend token validation başarısız: invalid response');
      }
      return null;
    }
  } catch (error: unknown) {
    if (isDev) {
      console.error('❌ Backend token validation hatası:', error);
    }

    const status = getHttpStatusFromError(error);

    // 401: do not retry /auth/me with the same session — clear storage and surface session expiry (web).
    if (status === 401) {
      if (isDev) {
        console.log('🚨 Token geçersiz (401), storage temizleniyor...');
      }
      await sessionManager.clearSession();
    } else if (status === 500) {
      if (isDev) {
        console.log('🚨 Backend hatası (500), storage temizleniyor...');
      }
      await sessionManager.clearSession();
    }

    return null;
  } finally {
    // Flag'i temizle
    (validateToken as any).isValidating = false;
  }
};

// Token'ı yenile
export const refreshToken = async (): Promise<string | null> => {
  try {
    return sessionManager.refreshAccessToken((refreshToken) =>
      apiClient.post<{ token: string }>('/auth/refresh', { refreshToken })
    );
  } catch (error) {
    if (isDev) {
      console.error('Refresh token error:', error);
    }
    await logout();
    return null;
  }
};

export async function changeMyPassword(
  currentPassword: string,
  newPassword: string,
): Promise<void> {
  await apiClient.put('/UserManagement/me/password', {
    currentPassword,
    newPassword,
  });
}

// Demo kullanıcı ile otomatik login
export const loginWithDemoUser = async (): Promise<LoginResponse> => {
  if (!isDev) {
    throw new Error('Demo login is disabled outside development.');
  }
  return await login(buildLoginPayload('cashier@demo.com', 'Cashier123!', 'pos'));
}; 